using Microsoft.Win32;
using SmartFoldering.Core;
using System.Runtime.Versioning;

namespace SmartFoldering.Services;

/// <summary>Registers the app in the per-user Windows "Run" key so it starts quietly (tray only) at sign-in.</summary>
public sealed class StartupService
{
    public const string MinimizedArgument = "--minimized";
    private const string AppName = "SmartFoldering";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(AppName) is not null;
        }
        catch (Exception ex)
        {
            Log.Warn("Cannot read the auto-start setting", ex);
            return false;
        }
    }

    public void SetEnabled(bool enable)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key is null) return;
            if (enable) key.SetValue(AppName, BuildCommand(CurrentExecutable()));
            else key.DeleteValue(AppName, false);
        }
        catch (Exception ex)
        {
            Log.Error("Cannot change the auto-start setting", ex);
        }
    }

    /// <summary>
    /// Keeps an existing auto-start entry pointing at the running executable, so moving or updating the app does not
    /// leave a dead entry behind. Version 1 used the same value name but wrote it once, without the tray-only argument;
    /// its entry is taken over here. Does nothing when auto-start is off.
    /// </summary>
    public void RepairIfMoved()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            Repair();
        }
        catch (Exception ex)
        {
            Log.Warn("Cannot check the auto-start entry", ex);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void Repair()
    {
        string expected = BuildCommand(CurrentExecutable());
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key?.GetValue(AppName) is string registered && !string.Equals(registered, expected, StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue(AppName, expected);
            Log.Info("Auto-start entry updated to the current executable path");
        }
    }

    internal static string BuildCommand(string exe) => $"\"{exe}\" {MinimizedArgument}";

    private static string CurrentExecutable() =>
        Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SmartFoldering.exe");
}
