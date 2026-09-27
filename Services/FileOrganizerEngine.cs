using SmartFoldering.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace SmartFoldering.Services;

public sealed class FileOrganizerEngine : IDisposable
{
    private readonly ISettingsManager _settingsManager;
    private readonly MoveJournal _journal;
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly HashSet<string> _pending = new(OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private Channel<string>? _queue;
    private CancellationTokenSource? _stop;
    private Task? _worker;
    private Timer? _fallbackTimer;
    private bool _running;

    public event Action<int>? OnScanCompleted;
    public event Action<string>? OnError;
    public bool IsRunning { get { lock (_gate) return _running; } }

    public FileOrganizerEngine(ISettingsManager settingsManager, MoveJournal? journal = null)
    {
        _settingsManager = settingsManager;
        _journal = journal ?? new MoveJournal();
    }

    public IReadOnlyList<PlannedMove> Preview() => MovePlanner.Preview(_settingsManager.GetCommittedSettings());
    public IReadOnlyList<MoveEvent> RecentMoves() => _journal.RecentMoves();
    public MoveEvent UndoLast()
    {
        if (IsRunning) throw new InvalidOperationException("Pause monitoring before undoing a move.");
        return _journal.UndoLast();
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            var settings = _settingsManager.GetCommittedSettings();
            MovePlanner.Validate(settings);
            _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true, SingleWriter = false
            });
            _stop = new CancellationTokenSource();
            _running = true;
            _worker = Task.Run(() => ConsumeAsync(_queue.Reader, _stop.Token));
            try
            {
                RestartWatchers();
                _fallbackTimer = new Timer(_ => Enqueue(string.Empty), null,
                    TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
                Enqueue(string.Empty);
            }
            catch
            {
                _running = false;
                _stop.Cancel();
                _queue.Writer.TryComplete();
                StopWatchers();
                var cancelled = _stop;
                _ = _worker?.ContinueWith(_ => cancelled.Dispose(), TaskScheduler.Default);
                _stop = null;
                _queue = null;
                _worker = null;
                _pending.Clear();
                throw;
            }
        }
    }

    public void Stop()
    {
        Task? worker;
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
            _fallbackTimer?.Dispose();
            _fallbackTimer = null;
            StopWatchers();
            _stop?.Cancel();
            _queue?.Writer.TryComplete();
            worker = _worker;
        }
        try { worker?.GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
        finally
        {
            lock (_gate)
            {
                _pending.Clear();
                _worker = null;
                _queue = null;
                _stop?.Dispose();
                _stop = null;
            }
        }
    }

    public void ForceScanNow()
    {
        if (!IsRunning) { OnError?.Invoke("Monitoring is paused. Review the preview, then start monitoring."); return; }
        Enqueue(string.Empty);
    }

    public void RestartWatchers()
    {
        lock (_gate)
        {
            if (!_running) return;
            var settings = _settingsManager.GetCommittedSettings();
            MovePlanner.Validate(settings);
            StopWatchers();
            foreach (var folder in settings.WatchFolders)
            {
                if (!Directory.Exists(folder.Path)) continue;
                var watcher = new FileSystemWatcher(folder.Path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024
                };
                watcher.Created += (_, e) => Enqueue(e.FullPath);
                watcher.Renamed += (_, e) => Enqueue(e.FullPath);
                watcher.Changed += (_, e) => Enqueue(e.FullPath);
                watcher.Error += (_, e) =>
                {
                    Report("File watcher missed events; a full scan was queued.");
                    Enqueue(string.Empty);
                };
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            Enqueue(string.Empty);
        }
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

    private void Enqueue(string path)
    {
        lock (_gate)
        {
            if (!_running || _queue == null || !_pending.Add(path)) return;
            if (!_queue.Writer.TryWrite(path)) _pending.Remove(path);
        }
    }

    private async Task ConsumeAsync(ChannelReader<string> reader, CancellationToken token)
    {
        try
        {
            await foreach (var path in reader.ReadAllAsync(token))
            {
                // Coalesce queued requests, while allowing a new scan when events arrive
                // after the active scan captured its file list (notably watcher overflow).
                lock (_gate) _pending.Remove(path);
                try
                {
                    if (path.Length == 0) await ScanAsync(token);
                    else await HandleFileAsync(path, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex) { Report(ex.Message); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task ScanAsync(CancellationToken token)
    {
        var settings = _settingsManager.GetCommittedSettings();
        var plans = MovePlanner.Preview(settings);
        var moved = 0;
        foreach (var plan in plans)
        {
            token.ThrowIfCancellationRequested();
            if (!await WaitReadyAsync(plan.Source, token)) continue;
            try { _journal.Move(plan, token); moved++; }
            catch (IOException ex) { Report($"Skipped {Path.GetFileName(plan.Source)}: {ex.Message}"); }
        }
        if (moved > 0) OnScanCompleted?.Invoke(moved);
    }

    private async Task HandleFileAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path) || !await WaitReadyAsync(path, token)) return;
        var settings = _settingsManager.GetCommittedSettings();
        MovePlanner.Validate(settings);
        foreach (var folder in settings.WatchFolders)
        {
            var plan = MovePlanner.PlanFile(settings, folder, path);
            if (plan == null) continue;
            try { _journal.Move(plan, token); OnScanCompleted?.Invoke(1); }
            catch (IOException ex) { Report($"Skipped {Path.GetFileName(path)}: {ex.Message}"); }
            return;
        }
    }

    private static async Task<bool> WaitReadyAsync(string path, CancellationToken token)
    {
        var until = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < until)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                long length;
                DateTime modified;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    length = stream.Length;
                    modified = File.GetLastWriteTimeUtc(path);
                }
                await Task.Delay(500, token);
                using var second = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                if (second.Length == length && File.GetLastWriteTimeUtc(path) == modified) return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (IOException) { await Task.Delay(500, token); }
        }
        return false;
    }

    private void Report(string message)
    {
        Log.Warning("SmartFoldering: {Message}", message);
        OnError?.Invoke(message);
    }

    public void Dispose() => Stop();
}
