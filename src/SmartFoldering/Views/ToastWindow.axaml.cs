using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace SmartFoldering.Views;

/// <summary>
/// A card in the top-right corner. Shown without taking focus (ShowActivated=False) so it never swallows keystrokes
/// meant for the window the user is typing in. It closes by itself, but not while the pointer is over it.
/// </summary>
public partial class ToastWindow : Window
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(7);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private TimeSpan _left = Lifetime;
    private bool _closing;

    public ToastWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) =>
        {
            if (IsPointerOver) return;
            _left -= _timer.Interval;
            if (_left <= TimeSpan.Zero) Dismiss();
        };
    }

    /// <summary>Shortens the remaining time, for example after Undo was pressed.</summary>
    public void CloseSoon(TimeSpan after) => _left = after;

    private void OnOpened(object? sender, EventArgs e)
    {
        if ((Screens.Primary ?? Screens.All.FirstOrDefault()) is { } screen)
        {
            double scale = screen.Scaling;
            var work = screen.WorkingArea;
            Position = new PixelPoint(work.Right - (int)Math.Ceiling(Width * scale), work.Y + (int)(8 * scale));
        }
        DispatcherTimer.RunOnce(() => Root.Classes.Set("hidden", false), TimeSpan.FromMilliseconds(30));
        _timer.Start();
    }

    /// <summary>Fades the toast out and closes it.</summary>
    public void Dismiss()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();
        Root.Classes.Set("hidden", true);
        DispatcherTimer.RunOnce(Close, TimeSpan.FromMilliseconds(260));
    }
}
