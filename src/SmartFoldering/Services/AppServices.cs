using SmartFoldering.Core;

namespace SmartFoldering.Services;

/// <summary>Everything the windows share: the settings, the move history, the engine and the auto-start entry.</summary>
public sealed class AppServices : IDisposable
{
    /// <summary>Overrides the data folder (tests and the screenshot tool use a temporary one).</summary>
    public const string DataDirVariable = "SMARTFOLDERING_DATA_DIR";

    public string DataFolder { get; }
    public SettingsStore Settings { get; }
    public MoveJournal Journal { get; }
    public Organizer Organizer { get; }
    public StartupService Startup { get; } = new();

    public AppServices(string? dataFolder = null, Func<DateTime>? clock = null)
    {
        DataFolder = dataFolder ?? DefaultDataFolder();
        Directory.CreateDirectory(DataFolder);
        Log.FilePath = Path.Combine(DataFolder, "smartfoldering.log");
        Settings = new SettingsStore(Path.Combine(DataFolder, "settings.json"));
        Journal = new MoveJournal(Path.Combine(DataFolder, "history.json"), clock);
        Organizer = new Organizer(Settings, Journal, clock);
    }

    /// <summary>%APPDATA%\SmartFoldering, the same folder version 1 used, so its settings are found and migrated.</summary>
    public static string DefaultDataFolder() =>
        Environment.GetEnvironmentVariable(DataDirVariable) is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFoldering");

    public void Dispose() => Organizer.Dispose();
}
