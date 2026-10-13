namespace SmartFoldering.Core;

/// <summary>
/// The sorting engine. File system notifications only put a file on a waiting list; a single background loop moves
/// it once it has stayed unchanged for the quiet period, is not a partial download or a placeholder, and can be
/// opened exclusively. A periodic full scan catches anything a notification missed. All moves happen on one thread,
/// against a private copy of the settings.
/// </summary>
public sealed class Organizer : IDisposable
{
    private static readonly TimeSpan LoopInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromHours(1);
    private const int MaxFailures = 5;

    private sealed class Pending
    {
        public required string FolderId;
        public DateTime FirstSeenUtc;
        public DateTime LastChangeUtc;
        public long Size;
        public DateTime WriteUtc;
        public int Failures;
        public bool Waiting;
    }

    private readonly SettingsStore _settings;
    private readonly MoveJournal _journal;
    private readonly Func<DateTime> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FileSystemWatcher> _watchers = new();
    private CancellationTokenSource? _loop;
    private DateTime _lastScanUtc = DateTime.MinValue;
    private string? _watchedKey;

    /// <summary>Raised on the engine's thread after each move.</summary>
    public event Action<MoveRecord>? FileSorted;

    /// <summary>Raised when something needs the user's attention (a folder vanished, a file cannot be moved).</summary>
    public event Action<string>? Problem;

    public Organizer(SettingsStore settings, MoveJournal journal, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _journal = journal;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public bool IsRunning => _loop is not null;

    public int PendingCount
    {
        get { lock (_gate) return _pending.Count; }
    }

    /// <summary>Files that are known to be busy: still growing, open in another program, or a placeholder.</summary>
    public int WaitingCount
    {
        get { lock (_gate) return _pending.Values.Count(p => p.Waiting); }
    }

    // ------------------------------------------------------------------ lifecycle

    public void Start()
    {
        if (_loop is not null) return;
        _settings.Changed += OnSettingsChanged;
        RestartWatchers();
        _loop = new CancellationTokenSource();
        var token = _loop.Token;
        _ = Task.Run(() => RunAsync(token), token);
        ScanNow();
    }

    public void Stop()
    {
        _settings.Changed -= OnSettingsChanged;
        _loop?.Cancel();
        _loop?.Dispose();
        _loop = null;
        lock (_gate)
        {
            StopWatchers();
            _watchedKey = null;
        }
    }

    public void Dispose() => Stop();

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LoopInterval, token).ConfigureAwait(false);
                var settings = _settings.Snapshot();
                if (_clock() - _lastScanUtc >= TimeSpan.FromMinutes(settings.RescanMinutes)) ScanNow();
                ProcessDue();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error("The sorting loop failed; it keeps running", ex);
            }
        }
    }

    // ------------------------------------------------------------------ watching

    /// <summary>Most changes (rules, pause, theme…) leave the watched folders alone; only a new set restarts the watchers.</summary>
    private void OnSettingsChanged()
    {
        if (RestartWatchers()) ScanNow();   // a folder that was just added or turned on is sorted right away
    }

    /// <summary>Watches every enabled folder. Returns false when that set did not change since the last call.</summary>
    private bool RestartWatchers()
    {
        var settings = _settings.Snapshot();
        var enabled = settings.WatchFolders.Where(f => f.Enabled).ToList();
        string key = string.Join("\n", enabled.Select(f => f.Id + "|" + f.Path));
        lock (_gate)
        {
            if (key == _watchedKey) return false;
            _watchedKey = key;
            StopWatchers();
            foreach (var folder in enabled)
            {
                if (!Directory.Exists(folder.Path))
                {
                    Problem?.Invoke($"folder-missing|{folder.Path}");
                    continue;
                }
                try
                {
                    var watcher = new FileSystemWatcher(folder.Path)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                        InternalBufferSize = 64 * 1024,
                        IncludeSubdirectories = false
                    };
                    string id = folder.Id;
                    watcher.Created += (_, e) => Enqueue(e.FullPath, id);
                    watcher.Changed += (_, e) => Enqueue(e.FullPath, id);
                    watcher.Renamed += (_, e) => Enqueue(e.FullPath, id);
                    watcher.Error += (_, e) =>
                    {
                        Log.Warn($"Watcher overflow on {folder.Path}; scanning", e.GetException());
                        ScanNow();
                    };
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                {
                    Log.Warn($"Cannot watch {folder.Path}", ex);
                    Problem?.Invoke($"folder-unwatchable|{folder.Path}");
                }
            }
        }
        return true;
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
    }

    /// <summary>Puts every file of every enabled folder on the waiting list.</summary>
    public void ScanNow()
    {
        _lastScanUtc = _clock();
        foreach (var folder in _settings.Snapshot().WatchFolders.Where(f => f.Enabled && Directory.Exists(f.Path)))
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder.Path)) Enqueue(file, folder.Id);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Cannot list {folder.Path}", ex);
            }
        }
    }

    /// <summary>Records that a file appeared or changed. Cheap and thread-safe: the file is only looked at later.</summary>
    public void Enqueue(string path, string folderId)
    {
        if (FileSafety.IsTemporary(path)) return;
        DateTime now = _clock();
        long size = -1;
        DateTime write = default;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return;
            size = info.Length;
            write = info.LastWriteTimeUtc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // decided later
        }

        lock (_gate)
        {
            if (_pending.TryGetValue(path, out var existing))
            {
                existing.LastChangeUtc = now;
                existing.FolderId = folderId;
            }
            else
            {
                _pending[path] = new Pending { FolderId = folderId, FirstSeenUtc = now, LastChangeUtc = now, Size = size, WriteUtc = write };
            }
        }
    }

    // ------------------------------------------------------------------ sorting

    /// <summary>Moves every waiting file whose quiet period is over. Returns the number of files moved.</summary>
    public int ProcessDue()
    {
        var settings = _settings.Snapshot();
        if (settings.Paused) return 0;

        DateTime now = _clock();
        var quiet = TimeSpan.FromSeconds(settings.QuietSeconds);
        List<KeyValuePair<string, Pending>> due;
        lock (_gate) due = _pending.Where(p => now - p.Value.LastChangeUtc >= quiet).ToList();

        int moved = 0;
        foreach (var (path, item) in due)
        {
            switch (TryMove(path, item, settings, now))
            {
                case Outcome.Moved:
                    moved++;
                    Forget(path);
                    break;
                case Outcome.Done:
                    Forget(path);
                    break;
                case Outcome.Wait:
                    lock (_gate)
                    {
                        item.LastChangeUtc = now;
                        item.Waiting = true;
                    }
                    if (now - item.FirstSeenUtc > GiveUpAfter) Forget(path);
                    break;
            }
        }
        return moved;
    }

    private enum Outcome { Moved, Done, Wait }

    private Outcome TryMove(string path, Pending item, AppSettings settings, DateTime now)
    {
        var folder = settings.WatchFolders.FirstOrDefault(f => f.Id == item.FolderId && f.Enabled);
        var file = new FileInfo(path);
        if (folder is null || !file.Exists) return Outcome.Done;

        switch (FileSafety.Inspect(file, now))
        {
            case SkipReason.Temporary or SkipReason.Hidden:
                return Outcome.Done;
            case SkipReason.Placeholder:
                return Outcome.Wait;
        }
        if (_journal.IsHeld(file.FullName)) return Outcome.Done;

        // Still growing or being rewritten: start the quiet period again.
        if (file.Length != item.Size || file.LastWriteTimeUtc != item.WriteUtc)
        {
            lock (_gate)
            {
                item.Size = file.Length;
                item.WriteUtc = file.LastWriteTimeUtc;
            }
            return Outcome.Wait;
        }

        var destination = RuleMatcher.FindDestination(folder, file.Name);
        if (destination is null || string.IsNullOrWhiteSpace(destination.Path)) return Outcome.Done;
        if (SafeMover.IsInside(destination.Path, folder.Path) && SafeMover.IsInside(folder.Path, destination.Path)) return Outcome.Done;
        if (!FileSafety.IsFree(file.FullName)) return Outcome.Wait;

        try
        {
            string? target = SafeMover.Move(file.FullName, destination.Path, settings.Conflict);
            if (target is null)
            {
                Log.Info($"Skipped {file.Name}: a file with that name is already in {destination.Path}");
                return Outcome.Done;
            }
            var record = _journal.Record(file.FullName, target, folder.Id, destination.Name);
            Log.Info($"Moved {file.Name} -> {target}");
            FileSorted?.Invoke(record);
            return Outcome.Moved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (++item.Failures >= MaxFailures)
            {
                Log.Warn($"Giving up on {file.Name}", ex);
                Problem?.Invoke($"move-failed|{file.Name}|{ex.Message}");
                return Outcome.Done;
            }
            return Outcome.Wait;
        }
    }

    private void Forget(string path)
    {
        lock (_gate) _pending.Remove(path);
    }

    /// <summary>What would move right now, without moving anything.</summary>
    public IReadOnlyList<PlannedMove> Preview(WatchFolder folder)
    {
        var plan = new List<PlannedMove>();
        if (!Directory.Exists(folder.Path)) return plan;
        DateTime now = _clock();
        try
        {
            foreach (string path in Directory.EnumerateFiles(folder.Path).Order(StringComparer.CurrentCultureIgnoreCase))
            {
                var file = new FileInfo(path);
                if (FileSafety.Inspect(file, now) != SkipReason.None || _journal.IsHeld(file.FullName)) continue;
                var destination = RuleMatcher.FindDestination(folder, file.Name);
                if (destination is not null && !string.IsNullOrWhiteSpace(destination.Path)) plan.Add(new PlannedMove(path, destination));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Cannot preview {folder.Path}", ex);
        }
        return plan;
    }
}
