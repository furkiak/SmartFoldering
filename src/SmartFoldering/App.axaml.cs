using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using SmartFoldering.Services;
using SmartFoldering.ViewModels;
using SmartFoldering.Views;

namespace SmartFoldering;

public partial class App : Application
{
    private static readonly TimeSpan BatchDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>Set by <see cref="Program"/> before the UI starts.</summary>
    public static SingleInstanceGuard? SingleInstance { get; set; }

    private AppServices? _services;
    private MainViewModel? _vm;
    private MainWindow? _main;
    private DispatcherTimer? _ticker;
    private TrayIcon? _tray;
    private NativeMenuItem? _openItem, _pauseItem, _scanItem, _settingsItem, _exitItem;
    private ToastWindow? _toast;
    private readonly List<MoveRecord> _batch = new();
    private DispatcherTimer? _batchTimer;
    private bool _trayHintShown, _shuttingDown;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A tray application: closing the window must never end the process, only "Quit" does.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DisableDataAnnotationValidation();
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Log.Error("Unhandled UI exception", e.Exception);
                e.Handled = true;
            };

            _services = new AppServices();
#if !DEBUG
            // Release builds only: a debug run from source must not repoint the installed app's auto-start entry.
            _services.Startup.RepairIfMoved();
#endif
            // Rules carried over from 1.x behave slightly differently (a keyword no longer matches the extension):
            // start paused so the user can look first.
            bool migrated = _services.Settings.Migrated;
            if (migrated) _services.Settings.Update(s => s.Paused = true);

            ApplyAppearance();
            _services.Settings.Changed += () => Dispatcher.UIThread.Post(ApplyAppearance);

            _vm = new MainViewModel(_services);
            _vm.StatusChanged += RefreshTray;
            _main = new MainWindow { DataContext = _vm, Services = _services };
            _main.HiddenToTray += OnHiddenToTray;

            _services.Organizer.FileSorted += record => Dispatcher.UIThread.Post(() => OnFileSorted(record));
            _services.Organizer.Start();

            _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticker.Tick += (_, _) =>
            {
                if (_main.IsVisible) _vm.Tick();
            };
            _ticker.Start();

            BuildTray();
            if (SingleInstance is not null) SingleInstance.ShowRequested += () => Dispatcher.UIThread.Post(ShowMain);
            desktop.Exit += (_, _) => Cleanup();

            bool minimized = desktop.Args?.Contains(StartupService.MinimizedArgument) == true;
            if (!minimized || migrated) ShowMain();
            if (migrated) ShowToast(new ToastViewModel(Loc.Instance.Get("toast.migrated.title"), Loc.Instance.Get("toast.migrated.body"), Look.Info, canShow: false));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyAppearance()
    {
        var settings = _services!.Settings.Snapshot();
        Loc.Instance.SetLanguage(settings.Language);
        RequestedThemeVariant = settings.Theme switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void ShowMain() => _main?.ShowFromTray();

    // ------------------------------------------------------------------ tray

    private void BuildTray()
    {
        _openItem = new NativeMenuItem();
        _openItem.Click += (_, _) => ShowMain();
        _pauseItem = new NativeMenuItem();
        _pauseItem.Click += (_, _) => _vm!.TogglePauseCommand.Execute(null);
        _scanItem = new NativeMenuItem();
        _scanItem.Click += (_, _) => _services!.Organizer.ScanNow();
        _settingsItem = new NativeMenuItem();
        _settingsItem.Click += async (_, _) => await _main!.OpenSettingsAsync();
        _exitItem = new NativeMenuItem();
        _exitItem.Click += (_, _) => Exit();

        using var icon = AssetLoader.Open(new Uri("avares://SmartFoldering/Assets/SmartFoldering.ico"));
        _tray = new TrayIcon
        {
            Icon = new WindowIcon(icon),
            Menu = new NativeMenu { _openItem, _pauseItem, _scanItem, _settingsItem, new NativeMenuItemSeparator(), _exitItem },
            IsVisible = true
        };
        _tray.Clicked += (_, _) => ShowMain();
        TrayIcon.SetIcons(this, new TrayIcons { _tray });

        Loc.Instance.LanguageChanged += RefreshTray;
        RefreshTray();
    }

    private void RefreshTray()
    {
        if (_openItem is null || _vm is null) return;
        var loc = Loc.Instance;
        _openItem.Header = loc.Get("tray.open");
        _pauseItem!.Header = loc.Get(_vm.IsPaused ? "tray.resume" : "tray.pause");
        _scanItem!.Header = loc.Get("tray.scan");
        _settingsItem!.Header = loc.Get("tray.settings");
        _exitItem!.Header = loc.Get("tray.exit");
        if (_tray is not null) _tray.ToolTipText = $"SmartFoldering · {_vm.StatusText}";
    }

    private void OnHiddenToTray()
    {
        if (_trayHintShown) return;
        _trayHintShown = true;
        ShowToast(new ToastViewModel(Loc.Instance.Get("toast.tray.title"), Loc.Instance.Get("toast.tray.body"), Look.Info, canShow: false));
    }

    // ------------------------------------------------------------------ toasts

    /// <summary>Moves arrive one by one; they are gathered for a moment so ten downloads make one card, not ten.</summary>
    private void OnFileSorted(MoveRecord record)
    {
        if (_services is null || !_services.Settings.Snapshot().Notifications) return;
        if (_main is { IsVisible: true, IsActive: true }) return;   // the activity list already shows it
        _batch.Add(record);
        if (_batchTimer is null)
        {
            _batchTimer = new DispatcherTimer { Interval = BatchDelay };
            _batchTimer.Tick += (_, _) => FlushBatch();
        }
        _batchTimer.Stop();
        _batchTimer.Start();
    }

    private void FlushBatch()
    {
        _batchTimer?.Stop();
        if (_batch.Count == 0) return;
        var loc = Loc.Instance;
        ToastViewModel toast;
        if (_batch.Count == 1)
        {
            var only = _batch[0];
            toast = new ToastViewModel(only.FileName, loc.Format("toast.one.body", only.DestinationName), Look.ForFile(only.FileName), only.Id);
        }
        else
        {
            var names = _batch.Select(r => r.DestinationName).Distinct().ToList();
            string into = string.Join(", ", names.Take(3)) + (names.Count > 3 ? "…" : "");
            toast = new ToastViewModel(loc.Format("toast.many.title", _batch.Count), loc.Format("toast.many.body", into), Look.Success);
        }
        _batch.Clear();
        ShowToast(toast);
    }

    private void ShowToast(ToastViewModel vm)
    {
        _toast?.Dismiss();
        var window = new ToastWindow { DataContext = vm };
        vm.DismissRequested += window.Dismiss;
        vm.ShowRequested += () =>
        {
            ShowMain();
            window.Dismiss();
        };
        vm.UndoRequested += () =>
        {
            if (vm.UndoId is null || _services is null) return;
            vm.Body = _services.Journal.Undo(vm.UndoId) switch
            {
                UndoResult.Undone or UndoResult.AlreadyUndone => Loc.Instance.Get("toast.undone"),
                UndoResult.FileMissing => Loc.Instance.Get("undo.missing"),
                _ => Loc.Instance.Get("undo.failed")
            };
            vm.CanUndo = false;
            window.CloseSoon(TimeSpan.FromSeconds(2));
        };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_toast, window)) _toast = null;
        };
        _toast = window;
        window.Show();
    }

    // ------------------------------------------------------------------ lifecycle

    private void Exit()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        _toast?.Close();
        _main?.CloseForShutdown();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    private void Cleanup()
    {
        _ticker?.Stop();
        _batchTimer?.Stop();
        _tray?.Dispose();
        _services?.Dispose();
    }

    private static void DisableDataAnnotationValidation()
    {
        foreach (var plugin in BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray())
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
