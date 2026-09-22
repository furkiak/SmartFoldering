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
 
        public static SettingsManager GlobalSettings { get; private set; }
        public static FileOrganizerEngine Engine { get; private set; }
        public static MainWindow AppMainWindow { get; private set; }
        public static IDialogService DialogService { get; private set; }  

      
        public override void Initialize() { AvaloniaXamlLoader.Load(this); }

        public override void OnFrameworkInitializationCompleted()
        {
        
            GlobalSettings = new SettingsManager();
            Engine = new FileOrganizerEngine(GlobalSettings);
            GlobalSettings.RulesChanging += () => Engine.Stop();
            DialogService = new DialogService();  
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
            Engine?.Stop();
            Environment.Exit(0);
        }
    }
}
