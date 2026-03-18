using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SmartFoldering.Models;
using SmartFoldering.Views;
using System.Threading.Tasks;

namespace SmartFoldering.Services
{
 
    public interface IDialogService
    {
        Task<(bool IsSaved, string Name, string Path)> ShowFolderDialogAsync(string name = "", string path = "");
        void OpenTargetFoldersWindow(WatchFolder folder);
        Task ShowSettingsWindowAsync();
    }

    public class DialogService : IDialogService
    {
      
        private Avalonia.Controls.Window GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        public async Task<(bool IsSaved, string Name, string Path)> ShowFolderDialogAsync(string name = "", string path = "")
        {
            var dialog = new FolderDialogWindow(name, path);
            var mainWindow = GetMainWindow();

            if (mainWindow != null)
                await dialog.ShowDialog(mainWindow);
            else
                dialog.Show();

            return (dialog.IsSaved, dialog.FolderName, dialog.FolderPath);
        }

        public void OpenTargetFoldersWindow(WatchFolder folder)
        {
            var mainWindow = GetMainWindow();
            var targetWindow = new TargetFoldersWindow(folder, App.GlobalSettings, mainWindow);

            mainWindow?.Hide();
            targetWindow.Show();
        }

        public async Task ShowSettingsWindowAsync()
        {
            var settingsWindow = new SettingsWindow();
            var mainWindow = GetMainWindow();
            if (mainWindow != null)
                await settingsWindow.ShowDialog(mainWindow);
        }
    }
}