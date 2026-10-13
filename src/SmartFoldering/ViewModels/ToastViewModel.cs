using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SmartFoldering.ViewModels;

/// <summary>A small card in the corner of the screen: what was sorted, with Undo for a single file.</summary>
public sealed partial class ToastViewModel : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _body;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private Look _look;

    public ToastViewModel(string title, string body, Look look, string? undoId = null, bool canShow = true)
    {
        _title = title;
        _body = body;
        _look = look;
        UndoId = undoId;
        _canUndo = undoId is not null;
        CanShow = canShow;
    }

    public string? UndoId { get; }
    public bool CanShow { get; }

    public event Action? UndoRequested;
    public event Action? ShowRequested;
    public event Action? DismissRequested;

    [RelayCommand]
    private void Undo() => UndoRequested?.Invoke();

    [RelayCommand]
    private void Show() => ShowRequested?.Invoke();

    [RelayCommand]
    private void Dismiss() => DismissRequested?.Invoke();
}
