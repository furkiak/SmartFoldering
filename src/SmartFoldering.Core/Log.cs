namespace SmartFoldering.Core;

/// <summary>A small file log that never throws. The file is rotated when it grows past 512 KB.</summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    /// <summary>The log file; null disables file output (tests).</summary>
    public static string? FilePath { get; set; }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{(ex is null ? "" : $" :: {ex.GetType().Name}: {ex.Message}")}";
        System.Diagnostics.Debug.WriteLine(line);
        if (FilePath is null) return;
        try
        {
            lock (Gate)
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxBytes) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // logging must never take the app down
        }
    }
}
