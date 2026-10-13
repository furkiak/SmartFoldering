namespace SmartFoldering.Core;

/// <summary>Why a file was left alone.</summary>
public enum SkipReason
{
    None,
    Temporary,      // a partial download, an editor's lock file, a system file
    Hidden,
    Placeholder,    // an empty file a browser created next to its partial download
    InUse,          // still being written or opened exclusively by another program
    Held            // the user undid this move recently
}

/// <summary>Recognises files that must not be moved yet (or ever).</summary>
public static class FileSafety
{
    /// <summary>Extensions of downloads and copies that are still in progress.</summary>
    public static readonly IReadOnlySet<string> PartialExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".partial", ".download", ".opdownload", ".tmp", ".temp", ".!ut", ".!qb", ".!bt",
        ".filepart", ".aria2", ".ytdl", ".dtapart", ".sync", ".dwl", ".dwl2", ".swp", ".lock", ".lck"
    };

    private static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini", "thumbs.db", "ehthumbs.db", ".ds_store", "icon\r"
    };

    /// <summary>An empty file younger than this is treated as a download placeholder.</summary>
    public static readonly TimeSpan PlaceholderAge = TimeSpan.FromMinutes(2);

    public static bool IsTemporary(string path)
    {
        string name = Path.GetFileName(path);
        return name.Length == 0
               || PartialExtensions.Contains(Path.GetExtension(name))
               || SystemNames.Contains(name)
               || name.StartsWith("~$", StringComparison.Ordinal)        // Office lock files
               || name.StartsWith(".~lock.", StringComparison.Ordinal);  // LibreOffice lock files
    }

    /// <summary>Checks everything that does not need to open the file.</summary>
    public static SkipReason Inspect(FileInfo file, DateTime nowUtc)
    {
        if (IsTemporary(file.Name)) return SkipReason.Temporary;
        if ((file.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) return SkipReason.Hidden;
        if (file.Length == 0 && (nowUtc - file.LastWriteTimeUtc < PlaceholderAge || HasPartialSibling(file))) return SkipReason.Placeholder;
        return SkipReason.None;
    }

    /// <summary>Firefox and Safari keep an empty "report.pdf" while "report.pdf.part" downloads.</summary>
    public static bool HasPartialSibling(FileInfo file) =>
        PartialExtensions.Any(ext => File.Exists(file.FullName + ext));

    /// <summary>True when nobody else has the file open for writing (it can be opened exclusively).</summary>
    public static bool IsFree(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Moves a file without ever overwriting another one.</summary>
public static class SafeMover
{
    private const int MaxAttempts = 1000;

    /// <summary>
    /// Moves <paramref name="source"/> into <paramref name="targetDirectory"/> and returns the new path, or null when
    /// the name is taken and the policy is <see cref="ConflictPolicy.Skip"/>. A taken name becomes "name (2).ext".
    /// File.Move without overwrite fails atomically when the name exists, so a file that appears in the meantime is
    /// never replaced.
    /// </summary>
    public static string? Move(string source, string targetDirectory, ConflictPolicy policy)
    {
        Directory.CreateDirectory(targetDirectory);
        string name = Path.GetFileName(source);
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            string candidate = Path.Combine(targetDirectory, attempt == 1 ? name : $"{stem} ({attempt}){extension}");
            try
            {
                File.Move(source, candidate, overwrite: false);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate) || Directory.Exists(candidate))
            {
                if (policy == ConflictPolicy.Skip) return null;
            }
        }
        throw new IOException($"No free name for {name} in {targetDirectory}.");
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> itself or inside it.</summary>
    public static bool IsInside(string path, string folder)
    {
        string a = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string b = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return a.Equals(b, comparison) || a.StartsWith(b + Path.DirectorySeparatorChar, comparison);
    }
}
