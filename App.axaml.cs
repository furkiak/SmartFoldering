using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SmartFoldering.Services;
using SmartFoldering.Views;
using System;

namespace SmartFoldering
{
    public partial class App : Application
    {
 
        private static SettingsManager? _globalSettings;
        private static FileOrganizerEngine? _engine;
        private static IDialogService? _dialogService;
        public static SettingsManager GlobalSettings => _globalSettings
            ?? throw new InvalidOperationException("Settings are not initialized.");
        public static FileOrganizerEngine Engine => _engine
            ?? throw new InvalidOperationException("The file engine is not initialized.");
        public static MainWindow? AppMainWindow { get; private set; }
        public static IDialogService DialogService => _dialogService
            ?? throw new InvalidOperationException("Dialogs are not initialized.");
        public static bool IsShuttingDown { get; private set; }

      
        public override void Initialize() { AvaloniaXamlLoader.Load(this); }

        public override void OnFrameworkInitializationCompleted()
        {
        
            _globalSettings = new SettingsManager();
            _engine = new FileOrganizerEngine(GlobalSettings);
            GlobalSettings.RulesChanging += () => Engine.Stop();
            _dialogService = new DialogService();
            Engine.OnScanCompleted += (movedCount) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    new NotificationWindow("Transaction Successful", $"{movedCount} files have been successfully moved to the target folders.").Show();
                });
            };

            Engine.OnError += (errorMessage) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    new NotificationWindow("System Alert", errorMessage).Show();
                });
            };

            if (GlobalSettings.GetCommittedSettings().MonitoringEnabled)
            {
                try { Engine.Start(); }
                catch (Exception ex)
                {
                    GlobalSettings.GetSettings().MonitoringEnabled = false;
                    new NotificationWindow("Monitoring paused", ex.Message).Show();
                }
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                AppMainWindow = new MainWindow();
                desktop.MainWindow = AppMainWindow;
                if (Engine.IsRunning)
                {
                    desktop.MainWindow.Hide();
                    new NotificationWindow("SmartFoldering is active", "Monitoring approved folders in the background.").Show();
                }
                else desktop.MainWindow.Show();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void Settings_Clicked(object sender, EventArgs e)
        {
            if (AppMainWindow != null)
            {
                AppMainWindow.Show();
                AppMainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                AppMainWindow.Activate();
            }
        }

        private void Exit_Clicked(object sender, EventArgs e)
        {
            RequestExit();
        }

        public static void RequestExit()
        {
            IsShuttingDown = true;
            _engine?.Stop();
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        }
    }
}
