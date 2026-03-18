using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmartFoldering.ViewModels;
using System;

namespace SmartFoldering.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;
        private bool _isFirstLaunch = true;

        public MainWindow()
        {
            InitializeComponent(); 
            DataContext = new MainWindowViewModel(App.GlobalSettings, App.DialogService);
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            PositionWindowBottomRight();

            if (_isFirstLaunch && ViewModel.WatchFolders.Count > 0)
            {
                this.Hide();
            }

            _isFirstLaunch = false;
        }

        private void PositionWindowBottomRight()
        {
            if (Screens.Primary != null)
            {
                var workArea = Screens.Primary.WorkingArea;
                var scaling = Screens.Primary.Scaling;

                int realWidth = (int)(this.Width * scaling);
                int realHeight = (int)(this.Height * scaling);

                int x = workArea.Right - realWidth - 20;
                int y = workArea.Bottom - realHeight - 20;

                this.Position = new Avalonia.PixelPoint(x, y);
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == WindowStateProperty && WindowState == WindowState.Minimized)
            {
                this.Hide();
                this.WindowState = WindowState.Normal;
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
         
        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private void HideWindow_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}