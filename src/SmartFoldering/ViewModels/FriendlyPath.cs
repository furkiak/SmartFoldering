namespace SmartFoldering.ViewModels;

/// <summary>
/// Paths as people read them: <c>C:\Users\ayla\Downloads\Documents</c> becomes <c>ayla › Downloads › Documents</c>,
/// like the Explorer address bar. Paths outside the user's profile are shown unchanged. The full path stays available
/// as a tooltip.
/// </summary>
public static class FriendlyPath
{
    /// <summary>The user's profile folder; the screenshot tool points it at its demo folder.</summary>
    public static string Home { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string Of(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(Home)) return path;
        string home = Path.TrimEndingDirectorySeparator(Home);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(home, comparison)) return path;

        string rest = path[home.Length..];
        if (rest.Length > 0 && rest[0] != Path.DirectorySeparatorChar && rest[0] != Path.AltDirectorySeparatorChar) return path;   // C:\Users\ayla2
        var parts = rest.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" › ", parts.Prepend(Path.GetFileName(home)));
    }
}
