using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SmartFoldering.Views;

/// <summary>Shows what would move. Closes with true when the user chose to start (or run) sorting.</summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
        Opened += (_, _) => WindowPlacement.ClampToWorkArea(this);
    }

    private void Start_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close(false);
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }
}
