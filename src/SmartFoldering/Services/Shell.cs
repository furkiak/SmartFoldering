using SmartFoldering.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartFoldering.Services;

/// <summary>Opens folders in the platform file manager and finds well-known folders. Failures are logged, never thrown.</summary>
public static partial class Shell
{
    public static void OpenFolder(string folder)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", [folder]);
            else
                Process.Start("xdg-open", [folder]);
        }
        catch (Exception ex)
        {
            Log.Warn("Cannot open a folder", ex);
        }
    }

    /// <summary>Opens the containing folder with the file selected.</summary>
    public static void Reveal(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                if (Path.GetDirectoryName(path) is { } parent && Directory.Exists(parent)) OpenFolder(parent);
                return;
            }
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", ["-R", path]);
            else
                OpenFolder(Path.GetDirectoryName(path)!);
        }
        catch (Exception ex)
        {
            Log.Warn("Cannot reveal a file", ex);
        }
    }

    /// <summary>The user's Downloads folder (it can be moved to another drive, so it is asked from Windows).</summary>
    public static string DownloadsFolder()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
                if (SHGetKnownFolderPath(downloads, 0, IntPtr.Zero, out IntPtr raw) == 0)
                {
                    try
                    {
                        if (Marshal.PtrToStringUni(raw) is { Length: > 0 } path) return path;
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(raw);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("Cannot ask Windows for the Downloads folder", ex);
            }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
