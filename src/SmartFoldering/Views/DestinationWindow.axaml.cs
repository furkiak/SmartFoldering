using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SmartFoldering.Localization;
using SmartFoldering.ViewModels;

namespace SmartFoldering.Views;

public partial class DestinationWindow : Window
{
    public DestinationWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            WindowPlacement.ClampToWorkArea(this);
            NameBox.Focus();
        };
    }

    private DestinationEditorViewModel? Vm => DataContext as DestinationEditorViewModel;

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm?.Commit() == true) Close();
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm?.RequestDelete() == true) Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        IStorageFolder? start = null;
        string current = Directory.Exists(vm.Path) ? vm.Path : vm.WatchedFolderPath;
        if (Directory.Exists(current)) start = await StorageProvider.TryGetFolderFromPathAsync(current);

        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Instance.Get("picker.destination"),
            AllowMultiple = false,
            SuggestedStartLocation = start
        });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
        {
            vm.Path = path;
            if (string.IsNullOrWhiteSpace(vm.Name)) vm.Name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
