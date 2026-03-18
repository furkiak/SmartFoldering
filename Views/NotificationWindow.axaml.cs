using Avalonia.Controls;
using Avalonia.Threading;
using System;

namespace SmartFoldering
{
    public partial class NotificationWindow : Window
    {
        private DispatcherTimer _closeTimer;

        public NotificationWindow()
        {
            InitializeComponent();
            this.Opened += (s, e) => PositionWindow();

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _closeTimer.Tick += (s, e) => { _closeTimer.Stop(); this.Close(); };
            _closeTimer.Start();
        }
         
        public NotificationWindow(string title, string message) : this()
        {
            TitleBlock.Text = title;
            MessageBlock.Text = message;
        }

        private void PositionWindow()
        {
            if (Screens.Primary != null)
            {
                var workArea = Screens.Primary.WorkingArea;
                var scaling = Screens.Primary.Scaling;

                int realWidth = (int)(this.Bounds.Width * scaling);

                int x = workArea.X + workArea.Width - realWidth - 20;
                int y = workArea.Y + 20;

                this.Position = new Avalonia.PixelPoint(x, y);
            }
        }
    }
}