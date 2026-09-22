using SmartFoldering.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartFoldering.Services;

public sealed record PlannedMove(string Source, string Destination, string WatchFolderId,
    string TargetFolderId, string RuleId, long SourceLength, DateTime SourceModifiedUtc);

public static class MovePlanner
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static string Canonical(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Source and destination paths must be absolute.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static void Validate(AppSettings settings)
    {
        if (settings.WatchFolders is null)
            throw new InvalidOperationException("Watched folders are missing from settings.");
        var sources = settings.WatchFolders.Select(folder => (Folder: folder, Path: Canonical(folder.Path))).ToArray();
        var dataFolder = Canonical(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFoldering"));
        if (sources.Any(x => Overlaps(x.Path, dataFolder)))
            throw new InvalidOperationException("The app's settings and journal folder cannot be watched.");
        foreach (var (_, source) in sources) EnsureNoLinks(source);
        for (var i = 0; i < sources.Length; i++)
        for (var j = i + 1; j < sources.Length; j++)
            if (Overlaps(sources[i].Path, sources[j].Path))
                throw new InvalidOperationException("Watched folders overlap; each file must have one owner.");

        foreach (var (folder, source) in sources)
        foreach (var target in folder.TargetFolders ?? throw new InvalidOperationException("Target folders are missing."))
        {
            var destination = Canonical(target.Path);
            EnsureNoLinks(destination);
            if (Overlaps(source, destination))
                throw new InvalidOperationException("A destination cannot be the same as, inside, or above its source.");
            if (sources.Any(other => ContainsOrSame(other.Path, destination)))
                throw new InvalidOperationException("A destination cannot feed another watched folder.");
            if (!SameVolume(source, destination))
                throw new InvalidOperationException("Automatic moves require source and destination on the same volume.");
            foreach (var rule in target.Rules ?? throw new InvalidOperationException("Rules are missing."))
                if (string.IsNullOrWhiteSpace(rule.Value))
                    throw new InvalidOperationException("An empty rule could move every file.");
        }
    }

    public static IReadOnlyList<PlannedMove> Preview(AppSettings settings)
    {
        Validate(settings);
        var reserved = new HashSet<string>(PathComparer);
        var planned = new List<PlannedMove>();
        foreach (var watch in settings.WatchFolders)
        {
            if (!Directory.Exists(watch.Path)) continue;
            foreach (var file in Directory.EnumerateFiles(watch.Path, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x, PathComparer))
            {
                var move = PlanFile(settings, watch, file, reserved);
                if (move != null) planned.Add(move);
            }
        }
        return planned;
    }

    public static PlannedMove? PlanFile(AppSettings settings, WatchFolder watch, string file, ISet<string>? reserved = null)
    {
        Validate(settings);
        var sourceRoot = Canonical(watch.Path);
        var source = Canonical(file);
        if (!string.Equals(Path.GetDirectoryName(source), sourceRoot, PathComparison) || !File.Exists(source)) return null;
        var info = new FileInfo(source);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget != null)
            throw new InvalidOperationException("Linked files are not moved automatically.");
        var name = Path.GetFileName(source);
        var extension = Path.GetExtension(source);
        if (extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".part", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".crdownload", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".download", StringComparison.OrdinalIgnoreCase)) return null;
        foreach (var target in watch.TargetFolders)
        foreach (var rule in target.Rules)
        {
            if (!Matches(rule, name, extension)) continue;
            var destinationDirectory = Canonical(target.Path);
            var destination = NextAvailable(destinationDirectory, name, reserved);
            reserved?.Add(destination);
            return new PlannedMove(source, destination, watch.Id, target.Id, rule.Id, info.Length, info.LastWriteTimeUtc);
        }
        return null;
    }

    public static bool IsStillCurrent(PlannedMove move)
    {
        if (!File.Exists(move.Source) || File.Exists(move.Destination)) return false;
        var file = new FileInfo(move.Source);
        return file.Length == move.SourceLength && file.LastWriteTimeUtc == move.SourceModifiedUtc &&
               (file.Attributes & FileAttributes.ReparsePoint) == 0 && file.LinkTarget == null;
    }

    private static bool Matches(Rule rule, string name, string extension) =>
        rule.Type == RuleType.Extension
            ? extension.Equals(rule.Value, StringComparison.OrdinalIgnoreCase)
            : rule.Type == RuleType.Keyword && name.Contains(rule.Value, StringComparison.OrdinalIgnoreCase);

    private static string NextAvailable(string directory, string name, ISet<string>? reserved)
    {
        var candidate = Path.Combine(directory, name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var index = 1; File.Exists(candidate) || reserved?.Contains(candidate) == true; index++)
            candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
        return candidate;
    }

    private static bool ContainsOrSame(string parent, string child) =>
        string.Equals(parent, child, PathComparison) ||
        child.StartsWith(parent + Path.DirectorySeparatorChar, PathComparison);

    private static bool Overlaps(string left, string right) =>
        ContainsOrSame(left, right) || ContainsOrSame(right, left);

    public static bool SameVolume(string left, string right) =>
        string.Equals(Path.GetPathRoot(Canonical(left)), Path.GetPathRoot(Canonical(right)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    public static void EnsureNoLinks(string path)
    {
        for (var current = path; current != null; current = Path.GetDirectoryName(current))
        {
            if (!Directory.Exists(current)) continue;
            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.LinkTarget != null)
                throw new InvalidOperationException("Linked or junction paths are not supported for watched folders or destinations.");
        }
    }
}
