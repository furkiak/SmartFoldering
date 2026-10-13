using System.Text.Json;

namespace SmartFoldering.Core;

public enum UndoResult { Undone, AlreadyUndone, NotFound, FileMissing, Failed }

/// <summary>
/// Remembers every move (history.json in the data folder) so it can be shown and undone. A file that was put back by
/// an undo is "held": the engine leaves it alone for a day instead of sorting it straight back.
/// </summary>
public sealed class MoveJournal
{
    public const int MaxEntries = 500;
    public static readonly TimeSpan HoldTime = TimeSpan.FromHours(24);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly object _gate = new();
    private readonly string? _path;
    private readonly List<MoveRecord> _entries;
    private readonly Func<DateTime> _clock;

    /// <summary>Raised after a move was recorded or undone.</summary>
    public event Action? Changed;

    public MoveJournal(string? path, Func<DateTime>? clock = null)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.UtcNow);
        _entries = Load(path);
    }

    public IReadOnlyList<MoveRecord> Recent(int count = 50)
    {
        lock (_gate) return _entries.AsEnumerable().Reverse().Take(count).ToList();
    }

    public int CountSince(DateTime utc)
    {
        lock (_gate) return _entries.Count(e => e.TimeUtc >= utc && e.UndoneUtc is null);
    }

    public MoveRecord Record(string from, string to, string watchFolderId, string destinationName)
    {
        var record = new MoveRecord(Guid.NewGuid().ToString("N"), _clock(), from, to, watchFolderId, destinationName);
        lock (_gate)
        {
            _entries.Add(record);
            if (_entries.Count > MaxEntries) _entries.RemoveRange(0, _entries.Count - MaxEntries);
            Save();
        }
        Changed?.Invoke();
        return record;
    }

    /// <summary>Moves the file back where it came from (under a free name if that one is taken now).</summary>
    public UndoResult Undo(string id)
    {
        UndoResult result;
        lock (_gate)
        {
            int index = _entries.FindIndex(e => e.Id == id);
            if (index < 0) return UndoResult.NotFound;
            var entry = _entries[index];
            if (entry.UndoneUtc is not null) return UndoResult.AlreadyUndone;
            if (!File.Exists(entry.To)) return UndoResult.FileMissing;

            try
            {
                string? back = SafeMover.Move(entry.To, Path.GetDirectoryName(entry.From)!, ConflictPolicy.KeepBoth);
                _entries[index] = entry with { From = back ?? entry.From, UndoneUtc = _clock() };
                Save();
                result = UndoResult.Undone;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn($"Undo of {entry.FileName} failed", ex);
                return UndoResult.Failed;
            }
        }
        Changed?.Invoke();
        return result;
    }

    /// <summary>True when the user put this file back recently: leave it where it is.</summary>
    public bool IsHeld(string path)
    {
        DateTime now = _clock();
        lock (_gate)
            return _entries.Any(e => e.UndoneUtc is { } undone && now - undone < HoldTime
                                     && string.Equals(e.From, path, StringComparison.OrdinalIgnoreCase));
    }

    private void Save()
    {
        if (_path is null) return;
        try
        {
            AtomicFile.Write(_path, JsonSerializer.Serialize(_entries, Json));
        }
        catch (Exception ex)
        {
            Log.Error("Saving the history failed", ex);
        }
    }

    private static List<MoveRecord> Load(string? path)
    {
        if (path is null || !File.Exists(path)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<MoveRecord>>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            Log.Warn("The history file is unreadable; starting a new one", ex);
            return new();
        }
    }
}

/// <summary>Writes through a temporary file so a crash never leaves a half-written file behind.</summary>
internal static class AtomicFile
{
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
