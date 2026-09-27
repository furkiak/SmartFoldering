using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace SmartFoldering.Services;

public sealed record MoveEvent(Guid Id, string Status, string Source, string Destination, string Sha256, DateTime AtUtc);

public sealed class MoveJournal
{
    private readonly string _path;
    private readonly object _gate = new();

    public MoveJournal(string? dataFolder = null)
    {
        var folder = dataFolder ?? AppStorage.Profile.Directory;
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "operations.jsonl");
    }

    public MoveEvent Move(PlannedMove plan, CancellationToken token = default)
    {
        lock (_gate)
        {
            _ = Read();
            token.ThrowIfCancellationRequested();
            if (!MovePlanner.IsStillCurrent(plan))
                throw new IOException("The source changed or destination now exists; review a fresh preview.");
            MovePlanner.EnsureNoLinks(plan.Source);
            MovePlanner.EnsureNoLinks(plan.Destination);
            if (!MovePlanner.SameVolume(plan.Source, plan.Destination))
                throw new IOException("Cross-volume moves are not automatic; choose a destination on the same volume.");
            var hash = Hash(plan.Source, token);
            token.ThrowIfCancellationRequested();
            if (!MovePlanner.IsStillCurrent(plan))
                throw new IOException("The source changed while it was being checked.");
            var operation = new MoveEvent(Guid.NewGuid(), "intent", plan.Source, plan.Destination, hash, DateTime.UtcNow);
            Append(operation);
            try
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.GetDirectoryName(plan.Destination)!);
                File.Move(plan.Source, plan.Destination);
                var completed = operation with { Status = "moved", AtUtc = DateTime.UtcNow };
                Append(completed);
                return completed;
            }
            catch
            {
                if (File.Exists(plan.Source)) Append(operation with { Status = "failed", AtUtc = DateTime.UtcNow });
                throw;
            }
        }
    }

    public MoveEvent UndoLast()
    {
        lock (_gate)
        {
            var events = Read();
            var active = events.GroupBy(x => x.Id).Select(group => group.Last())
                .Where(x => x.Status == "moved" ||
                    (x.Status == "intent" && !File.Exists(x.Source) && File.Exists(x.Destination)))
                .OrderByDescending(x => x.AtUtc).FirstOrDefault()
                ?? throw new InvalidOperationException("No recorded move is available to undo.");
            if (File.Exists(active.Source) || !File.Exists(active.Destination))
                throw new IOException("The source is occupied or destination is missing; nothing was overwritten.");
            MovePlanner.EnsureNoLinks(active.Source);
            MovePlanner.EnsureNoLinks(active.Destination);
            if (Hash(active.Destination) != active.Sha256)
                throw new IOException("The moved file changed after the operation; undo refused to protect it.");
            File.Move(active.Destination, active.Source);
            var undone = active with { Status = "undone", AtUtc = DateTime.UtcNow };
            Append(undone);
            return undone;
        }
    }

    public IReadOnlyList<MoveEvent> RecentMoves(int count = 20)
    {
        lock (_gate) return Read().GroupBy(x => x.Id).Select(group => group.Last())
            .TakeLast(count).Reverse().ToList();
    }

    private List<MoveEvent> Read()
    {
        if (!File.Exists(_path)) return new List<MoveEvent>();
        var result = new List<MoveEvent>();
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var entry = JsonSerializer.Deserialize<MoveEvent>(line)
                ?? throw new InvalidDataException("The operation journal is damaged; moves and undo are disabled.");
            if (entry.Id == Guid.Empty ||
                entry.Status is not ("intent" or "moved" or "failed" or "undone") ||
                string.IsNullOrWhiteSpace(entry.Source) || string.IsNullOrWhiteSpace(entry.Destination) ||
                entry.Sha256?.Length != 64)
                throw new InvalidDataException("The operation journal is damaged; moves and undo are disabled.");
            result.Add(entry);
        }
        return result;
    }

    private void Append(MoveEvent entry)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
        using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string Hash(string path, CancellationToken token = default)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            token.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, read);
        }
        token.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash.GetHashAndReset());
    }

}
