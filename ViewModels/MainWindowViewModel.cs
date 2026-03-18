using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFoldering.Models;
using SmartFoldering.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace SmartFoldering.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ISettingsManager _settingsManager;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<WatchFolder> _watchFolders;

        public MainWindowViewModel() { }

   
        public MainWindowViewModel(ISettingsManager settingsManager, IDialogService dialogService)
        {
            _settingsManager = settingsManager;
            _dialogService = dialogService;
            LoadData();
        }

        private void LoadData()
        {
            var settings = _settingsManager.GetSettings();
            WatchFolders = new ObservableCollection<WatchFolder>(settings.WatchFolders);
        }

    

        [RelayCommand]
        private async Task AddWatchFolderAsync()
        {
          
            var result = await _dialogService.ShowFolderDialogAsync();
            if (result.IsSaved)
            {
                var newFolder = new WatchFolder { Name = result.Name, Path = result.Path };
                WatchFolders.Add(newFolder);
                _settingsManager.GetSettings().WatchFolders.Add(newFolder);
                await _settingsManager.SaveSettingsAsync();
                App.Engine.RestartWatchers();
            }
        }

        [RelayCommand]
        private async Task EditWatchFolderAsync(WatchFolder folder)
        {
            if (folder == null) return;

            var result = await _dialogService.ShowFolderDialogAsync(folder.Name, folder.Path);
            if (result.IsSaved)
            {
                folder.Name = result.Name;
                folder.Path = result.Path;
                await _settingsManager.SaveSettingsAsync();
 
                var tempList = new ObservableCollection<WatchFolder>(WatchFolders);
                WatchFolders = tempList;
            }
        }

        [RelayCommand]
        private async Task DeleteWatchFolderAsync(WatchFolder folder)
        {
            if (folder != null)
            {
                WatchFolders.Remove(folder);
                _settingsManager.GetSettings().WatchFolders.Remove(folder);
                await _settingsManager.SaveSettingsAsync();
                App.Engine.RestartWatchers();
            }
        }

        [RelayCommand]
        private void OpenDestinations(WatchFolder folder)
        {
            if (folder != null)
            {
                _dialogService.OpenTargetFoldersWindow(folder);
            }
        }

        [RelayCommand]
        private async Task OpenSettingsAsync()
        {
            await _dialogService.ShowSettingsWindowAsync();
        }

        [RelayCommand]
        private void ForceScan()
        {
            App.Engine.ForceScanNow();
        }
    }
}