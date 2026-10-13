using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using SmartFoldering.Services;
using SmartFoldering.ViewModels;

namespace SmartFoldering.Views;

public partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    /// <summary>Raised when the close button sent the window to the tray.</summary>
    public event Action? HiddenToTray;

    /// <summary>Settings need the services; the app sets this before the window is shown.</summary>
    public AppServices? Services { get; set; }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // ------------------------------------------------------------------ show / hide

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Root.Classes.Set("hidden", true);
            Show();
            DispatcherTimer.RunOnce(() => Root.Classes.Set("hidden", false), TimeSpan.FromMilliseconds(30));
        }
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Vm?.Tick();
        Activate();
    }

    /// <summary>Really closes the window (application shutdown); the close button only hides it.</summary>
    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
        HiddenToTray?.Invoke();
    }

    private void OnDragAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else BeginMoveDrag(e);
    }

    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginResizeDrag(WindowEdge.SouthEast, e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ watched folders

    private async void AddDownloads_Click(object? sender, RoutedEventArgs e) =>
        await AddFolderAsync(Shell.DownloadsFolder(), withTemplates: true, Loc.Instance.Get("folder.downloads"));

    private async void AddWithTemplates_Click(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync(Loc.Instance.Get("picker.watch")) is { } path) await AddFolderAsync(path, withTemplates: true);
    }

    private async void AddEmpty_Click(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync(Loc.Instance.Get("picker.watch")) is { } path) await AddFolderAsync(path, withTemplates: false);
    }

    /// <summary>Adds the folder switched off, then shows what would move (or opens the editor for a folder without rules).</summary>
    public async Task AddFolderAsync(string path, bool withTemplates, string? name = null)
    {
        if (Vm is not { } vm) return;
        if (vm.AddFolder(path, withTemplates, name) is { } error)
        {
            vm.NoticeText = Loc.Instance.Get(error);
            return;
        }
        if (withTemplates) await OpenPreviewAsync();
        else await EditDestinationAsync(null);
    }

    private async void Preview_Click(object? sender, RoutedEventArgs e) => await OpenPreviewAsync();

    private async Task OpenPreviewAsync()
    {
        if (Vm?.CreatePreview() is not { } preview) return;
        bool? start = await new PreviewWindow { DataContext = preview }.ShowDialog<bool?>(this);
        if (start == true) Vm.StartSorting();
    }

    // ------------------------------------------------------------------ destinations

    private async void AddDestination_Click(object? sender, RoutedEventArgs e) => await EditDestinationAsync(null);

    private async void EditDestination_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DestinationItem item }) await EditDestinationAsync(item.Model);
    }

    private async void Destination_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: DestinationItem item }) await EditDestinationAsync(item.Model);
    }

    private async Task EditDestinationAsync(Destination? existing)
    {
        if (Vm is not { SelectedFolder: { } folder } vm) return;
        var editor = new DestinationEditorViewModel(folder.Model, existing);
        await new DestinationWindow { DataContext = editor }.ShowDialog(this);
        if (editor.Deleted && existing is not null) vm.DeleteDestination(existing.Id);
        else if (editor.Result is { } saved) vm.SaveDestination(saved);
    }

    private void MoveUp_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DestinationItem item }) Vm?.MoveUpCommand.Execute(item);
    }

    private void MoveDown_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: DestinationItem item }) Vm?.MoveDownCommand.Execute(item);
    }

    // ------------------------------------------------------------------ activity

    private void Undo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ActivityItem item }) Vm?.UndoCommand.Execute(item);
    }

    private void Reveal_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ActivityItem item }) Vm?.RevealCommand.Execute(item);
    }

    // ------------------------------------------------------------------ settings

    private async void OpenSettings_Click(object? sender, RoutedEventArgs e) => await OpenSettingsAsync();

    public async Task OpenSettingsAsync()
    {
        if (Services is null) return;
        if (!IsVisible) ShowFromTray();
        var vm = new SettingsViewModel(Services);
        try
        {
            await new SettingsWindow { DataContext = vm }.ShowDialog(this);
        }
        finally
        {
            vm.Detach();
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
