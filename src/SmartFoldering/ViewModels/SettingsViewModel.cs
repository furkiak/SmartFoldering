using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using SmartFoldering.Services;
using System.Reflection;

namespace SmartFoldering.ViewModels;

/// <summary>The settings dialog. Every change is saved at once; there is no Cancel.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly string[] Languages = ["auto", "en", "tr"];
    private static readonly string[] Themes = ["system", "light", "dark"];

    private readonly AppServices _services;

    [ObservableProperty] private int _languageIndex;
    [ObservableProperty] private int _themeIndex;
    [ObservableProperty] private int _conflictIndex;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(QuietText))] private double _quietSeconds;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RescanText))] private double _rescanMinutes;
    [ObservableProperty] private bool _notifications;
    [ObservableProperty] private bool _startWithWindows;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        var s = services.Settings.Snapshot();
        _languageIndex = Math.Max(0, Array.IndexOf(Languages, s.Language));
        _themeIndex = Math.Max(0, Array.IndexOf(Themes, s.Theme));
        _conflictIndex = s.Conflict == ConflictPolicy.Skip ? 1 : 0;
        _quietSeconds = s.QuietSeconds;
        _rescanMinutes = s.RescanMinutes;
        _notifications = s.Notifications;
        _startWithWindows = services.Startup.IsEnabled();
        Loc.Instance.LanguageChanged += OnLanguageChanged;
    }

    public bool IsStartupSupported => _services.Startup.IsSupported;
    public string QuietText => Loc.Instance.Format("settings.quiet.value", (int)QuietSeconds);
    public string RescanText => Loc.Instance.Format("settings.rescan.value", (int)RescanMinutes);
    public string VersionText => Loc.Instance.Format("settings.version", AppVersion);

    public static string AppVersion =>
        typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "2";

    partial void OnLanguageIndexChanged(int value) => Save(s => s.Language = Languages[Math.Clamp(value, 0, 2)]);
    partial void OnThemeIndexChanged(int value) => Save(s => s.Theme = Themes[Math.Clamp(value, 0, 2)]);
    partial void OnConflictIndexChanged(int value) => Save(s => s.Conflict = value == 1 ? ConflictPolicy.Skip : ConflictPolicy.KeepBoth);
    partial void OnQuietSecondsChanged(double value) => Save(s => s.QuietSeconds = (int)Math.Round(value));
    partial void OnRescanMinutesChanged(double value) => Save(s => s.RescanMinutes = (int)Math.Round(value));
    partial void OnNotificationsChanged(bool value) => Save(s => s.Notifications = value);
    partial void OnStartWithWindowsChanged(bool value) => _services.Startup.SetEnabled(value);

    [RelayCommand]
    private void OpenDataFolder() => Shell.OpenFolder(_services.DataFolder);

    private void Save(Action<AppSettings> edit) => _services.Settings.Update(edit);

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(QuietText));
        OnPropertyChanged(nameof(RescanText));
        OnPropertyChanged(nameof(VersionText));
    }

    /// <summary>Called when the dialog closes.</summary>
    public void Detach() => Loc.Instance.LanguageChanged -= OnLanguageChanged;
}
