using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using SmartFoldering.Services;
using System.Collections.ObjectModel;

namespace SmartFoldering.ViewModels;

/// <summary>
/// The main window: watched folders on the left, the selected folder's destinations in the middle, recent moves on
/// the right. All edits go through <see cref="SettingsStore.Update"/>; the store's Changed event brings them back here,
/// so the engine, the tray and this window always show the same state.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int ActivityCount = 60;

    private readonly AppServices _services;
    private readonly Func<DateTime> _clock;
    private bool _syncing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ShowOffBanner), nameof(ShowMissingBanner),
                              nameof(SelectedName), nameof(SelectedPath), nameof(SelectedDisplayPath), nameof(SelectedExists))]
    private WatchFolderItem? _selectedFolder;

    [ObservableProperty] private bool _selectedEnabled;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _sortedToday = "0";
    [ObservableProperty] private string _waitingText = "";
    [ObservableProperty] private string _activityTodayText = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasProblem))] private string? _problemText;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasNotice))] private string? _noticeText;

    public MainViewModel(AppServices services, Func<DateTime>? clock = null)
    {
        _services = services;
        _clock = clock ?? (() => DateTime.UtcNow);
        services.Settings.Changed += () => OnUi(SyncFromSettings);
        services.Journal.Changed += () => OnUi(RefreshActivity);
        services.Organizer.Problem += problem => OnUi(() => ShowProblem(problem));
        Loc.Instance.LanguageChanged += () =>
        {
            SyncFromSettings();
            RefreshActivity();
        };
        SyncFromSettings();
        RefreshActivity();
    }

    public ObservableCollection<WatchFolderItem> Folders { get; } = new();
    public ObservableCollection<DestinationItem> Destinations { get; } = new();
    public ObservableCollection<ActivityItem> Activity { get; } = new();

    public bool HasFolders => Folders.Count > 0;
    public bool HasSelection => SelectedFolder is not null;
    public bool HasActivity => Activity.Count > 0;
    public bool HasProblem => ProblemText is not null;
    public bool HasNotice => NoticeText is not null;
    public bool ShowOffBanner => SelectedFolder is { Enabled: false, Exists: true };
    public bool ShowMissingBanner => SelectedFolder is { Exists: false };
    public string SelectedName => SelectedFolder?.Name ?? "";
    public string SelectedPath => SelectedFolder?.Path ?? "";
    public string SelectedDisplayPath => SelectedFolder?.DisplayPath ?? "";
    public bool SelectedExists => SelectedFolder?.Exists ?? false;
    public string PauseTip => Loc.Instance.Get(IsPaused ? "tip.resume" : "tip.pause");

    /// <summary>Raised when the tray tooltip should change.</summary>
    public event Action? StatusChanged;

    // ------------------------------------------------------------------ state from the store

    private void SyncFromSettings()
    {
        var settings = _services.Settings.Snapshot();
        _syncing = true;
        try
        {
            for (int i = Folders.Count - 1; i >= 0; i--)
                if (settings.WatchFolders.All(f => f.Id != Folders[i].Id))
                    Folders.RemoveAt(i);

            for (int i = 0; i < settings.WatchFolders.Count; i++)
            {
                var folder = settings.WatchFolders[i];
                int existing = Folders.ToList().FindIndex(f => f.Id == folder.Id);
                if (existing < 0)
                {
                    Folders.Insert(i, new WatchFolderItem(folder));
                    continue;
                }
                if (existing != i) Folders.Move(existing, i);
                Folders[i].Update(folder);
            }

            if (SelectedFolder is null || !Folders.Contains(SelectedFolder)) SelectedFolder = Folders.FirstOrDefault();
            SelectedEnabled = SelectedFolder?.Enabled ?? false;
            IsPaused = settings.Paused;
            RebuildDestinations();
        }
        finally
        {
            _syncing = false;
        }

        int watching = settings.WatchFolders.Count(f => f.Enabled);
        IsActive = watching > 0 && !settings.Paused;
        StatusText = settings.Paused ? Loc.Instance.Get("status.paused")
                   : settings.WatchFolders.Count == 0 ? Loc.Instance.Get("status.idle")
                   : Loc.Instance.Count("status.watching", watching);
        foreach (string name in new[] { nameof(HasFolders), nameof(ShowOffBanner), nameof(ShowMissingBanner), nameof(PauseTip),
                                        nameof(SelectedName), nameof(SelectedPath), nameof(SelectedDisplayPath), nameof(SelectedExists) })
            OnPropertyChanged(name);
        RefreshStats();
        StatusChanged?.Invoke();
    }

    partial void OnSelectedFolderChanged(WatchFolderItem? value)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            SelectedEnabled = value?.Enabled ?? false;
            RebuildDestinations();
        }
        finally
        {
            _syncing = false;
        }
    }

    partial void OnSelectedEnabledChanged(bool value)
    {
        if (_syncing || SelectedFolder is not { } folder) return;
        UpdateFolder(folder.Id, f => f.Enabled = value);
    }

    private void RebuildDestinations()
    {
        Destinations.Clear();
        if (SelectedFolder is not { } folder) return;
        var list = folder.Model.Destinations;
        for (int i = 0; i < list.Count; i++) Destinations.Add(new DestinationItem(list[i], i, list.Count));
    }

    private void RefreshActivity()
    {
        DateTime now = _clock();
        Activity.Clear();
        foreach (var record in _services.Journal.Recent(ActivityCount)) Activity.Add(new ActivityItem(record, now));
        OnPropertyChanged(nameof(HasActivity));
        RefreshStats();
    }

    /// <summary>Counters that change without an event (files waiting, relative times). Called once a second.</summary>
    public void Tick()
    {
        RefreshStats();
        DateTime now = _clock();
        foreach (var item in Activity) item.Refresh(now);
    }

    private void RefreshStats()
    {
        int today = _services.Journal.CountSince(_clock().ToLocalTime().Date.ToUniversalTime());
        SortedToday = today.ToString(Loc.Instance.Culture);
        ActivityTodayText = Loc.Instance.Format("activity.today", today);
        int waiting = _services.Organizer.WaitingCount;
        WaitingText = waiting == 0 ? Loc.Instance.Get("stats.waiting.none") : Loc.Instance.Count("stats.waiting", waiting);
    }

    private void ShowProblem(string problem)
    {
        string[] parts = problem.Split('|');
        if (parts[0] == "folder-missing")
        {
            SyncFromSettings();   // the sidebar marks the folder and its page explains it; no second banner
            return;
        }
        ProblemText = Loc.Instance.Format("problem." + parts[0], parts.Skip(1).Cast<object>().ToArray());
    }

    // ------------------------------------------------------------------ commands

    [RelayCommand]
    private void TogglePause() => _services.Settings.Update(s => s.Paused = !s.Paused);

    [RelayCommand]
    private void ScanNow() => _services.Organizer.ScanNow();

    [RelayCommand]
    private void OpenSelectedFolder()
    {
        if (SelectedFolder is { Exists: true } folder) Shell.OpenFolder(folder.Path);
    }

    [RelayCommand]
    private void RemoveSelectedFolder()
    {
        if (SelectedFolder is not { } folder) return;
        _services.Settings.Update(s => s.WatchFolders.RemoveAll(f => f.Id == folder.Id));
    }

    [RelayCommand]
    private void MoveUp(DestinationItem item) => MoveDestination(item, -1);

    [RelayCommand]
    private void MoveDown(DestinationItem item) => MoveDestination(item, +1);

    private void MoveDestination(DestinationItem item, int delta)
    {
        if (SelectedFolder is not { } folder) return;
        UpdateFolder(folder.Id, f =>
        {
            int index = f.Destinations.FindIndex(d => d.Id == item.Id);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= f.Destinations.Count) return;
            (f.Destinations[index], f.Destinations[target]) = (f.Destinations[target], f.Destinations[index]);
        });
    }

    [RelayCommand]
    private void Undo(ActivityItem item)
    {
        NoticeText = _services.Journal.Undo(item.Record.Id) switch
        {
            UndoResult.FileMissing => Loc.Instance.Get("undo.missing"),
            UndoResult.Failed => Loc.Instance.Get("undo.failed"),
            _ => null
        };
    }

    [RelayCommand]
    private void Reveal(ActivityItem item) => Shell.Reveal(item.IsUndone ? item.Record.From : item.Record.To);

    [RelayCommand]
    private void DismissProblem() => ProblemText = null;

    [RelayCommand]
    private void DismissNotice() => NoticeText = null;

    // ------------------------------------------------------------------ edits requested by the window

    /// <summary>
    /// Starts watching a folder. It is added switched off: the window shows a preview first, and nothing moves until
    /// the user turns sorting on. Returns a localisation key when the folder cannot be added.
    /// </summary>
    public string? AddFolder(string path, bool withTemplates, string? name = null)
    {
        string full = Normalize(path);
        var settings = _services.Settings.Snapshot();
        if (settings.WatchFolders.Any(f => SamePath(f.Path, full))) return "folder.exists";
        if (settings.WatchFolders.SelectMany(f => f.Destinations).Any(d => SamePath(d.Path, full))) return "folder.inside";

        var folder = new WatchFolder
        {
            Name = name ?? (System.IO.Path.GetFileName(full) is { Length: > 0 } leaf ? leaf : full),
            Path = full,
            Enabled = false,
            Destinations = withTemplates ? RuleTemplates.Create(full, key => Loc.Instance.Get("template." + key)) : new()
        };
        _services.Settings.Update(s => s.WatchFolders.Add(folder));
        SelectedFolder = Folders.FirstOrDefault(f => f.Id == folder.Id);
        return null;
    }

    public void SaveDestination(Destination destination)
    {
        if (SelectedFolder is not { } folder) return;
        UpdateFolder(folder.Id, f =>
        {
            int index = f.Destinations.FindIndex(d => d.Id == destination.Id);
            if (index >= 0) f.Destinations[index] = destination;
            else f.Destinations.Add(destination);
        });
    }

    public void DeleteDestination(string id)
    {
        if (SelectedFolder is { } folder) UpdateFolder(folder.Id, f => f.Destinations.RemoveAll(d => d.Id == id));
    }

    /// <summary>What would move in the selected folder right now.</summary>
    public PreviewViewModel? CreatePreview() =>
        SelectedFolder is { } folder ? new PreviewViewModel(folder.Model, _services.Organizer.Preview(folder.Model)) : null;

    /// <summary>The preview's main button: turns sorting on (which queues the folder's files) or scans again.</summary>
    public void StartSorting()
    {
        if (SelectedFolder is not { } folder) return;
        if (folder.Enabled) _services.Organizer.ScanNow();
        else UpdateFolder(folder.Id, f => f.Enabled = true);
        if (IsPaused) _services.Settings.Update(s => s.Paused = false);
    }

    private void UpdateFolder(string id, Action<WatchFolder> edit) =>
        _services.Settings.Update(s =>
        {
            if (s.WatchFolders.FirstOrDefault(f => f.Id == id) is { } folder) edit(folder);
        });

    internal static string Normalize(string path) => System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path.Trim()));

    internal static bool SamePath(string a, string b) =>
        string.Equals(Normalize(a), Normalize(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}
