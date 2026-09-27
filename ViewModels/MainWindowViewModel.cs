using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartFoldering.Models;
using SmartFoldering.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;
using Avalonia.Threading;

namespace SmartFoldering.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly ISettingsManager _settingsManager = null!;
        private readonly IDialogService _dialogService = null!;
        private int _previewRevision = -1;

        [ObservableProperty] private string _statusText = "Monitoring paused · preview moves before starting";
        [ObservableProperty] private string _monitoringButtonText = "Start monitoring";
        [ObservableProperty] private string _previewText = "Choose Preview to see proposed moves. No file is changed by preview.";
        [ObservableProperty] private string _recentMoveText = "No moves in this session.";

        [ObservableProperty]
        private ObservableCollection<WatchFolder> _watchFolders = new();

        public MainWindowViewModel() { }

   
        public MainWindowViewModel(ISettingsManager settingsManager, IDialogService dialogService)
        {
            _settingsManager = settingsManager;
            _dialogService = dialogService;
            LoadData();
            MonitoringButtonText = App.Engine.IsRunning ? "Pause monitoring" : "Start monitoring";
            StatusText = App.Engine.IsRunning ? "Monitoring approved folders" : "Monitoring paused · preview moves before starting";
            RefreshRecent();
            App.Engine.OnScanCompleted += count => Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Moved {count} file(s). Pause monitoring before undo.";
                RefreshRecent();
            });
            if (settingsManager is SettingsManager concrete)
                concrete.RulesChanging += () => Dispatcher.UIThread.Post(() =>
                {
                    MonitoringButtonText = "Start monitoring";
                    StatusText = "Rules changed. Monitoring paused; preview again before starting.";
                    InvalidatePreview();
                });
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
                try
                {
                    var newFolder = new WatchFolder { Name = result.Name, Path = result.Path };
                    _settingsManager.GetSettings().WatchFolders.Add(newFolder);
                    await _settingsManager.SaveSettingsAsync();
                    LoadData(); App.Engine.RestartWatchers(); InvalidatePreview();
                }
                catch (Exception ex) { LoadData(); StatusText = "Folder not saved: " + ex.Message; }
            }
        }

        [RelayCommand]
        private async Task EditWatchFolderAsync(WatchFolder folder)
        {
            if (folder == null) return;

            var result = await _dialogService.ShowFolderDialogAsync(folder.Name, folder.Path);
            if (result.IsSaved)
            {
                try
                {
                    folder.Name = result.Name;
                    folder.Path = result.Path;
                    await _settingsManager.SaveSettingsAsync();
                    LoadData(); App.Engine.RestartWatchers(); InvalidatePreview();
                }
                catch (Exception ex) { LoadData(); StatusText = "Folder not saved: " + ex.Message; }
            }
        }

        [RelayCommand]
        private async Task DeleteWatchFolderAsync(WatchFolder folder)
        {
            if (folder != null)
            {
                try
                {
                    _settingsManager.GetSettings().WatchFolders.Remove(folder);
                    await _settingsManager.SaveSettingsAsync();
                    LoadData(); App.Engine.RestartWatchers(); InvalidatePreview();
                }
                catch (Exception ex) { LoadData(); StatusText = "Folder not deleted: " + ex.Message; }
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

        [RelayCommand]
        private void Preview()
        {
            try
            {
                var moves = App.Engine.Preview();
                _previewRevision = _settingsManager.Revision;
                PreviewText = moves.Count == 0 ? "No matching files are waiting. Rules are safe to start." :
                    string.Join(Environment.NewLine, moves.Select(m =>
                        $"{Path.GetFileName(m.Source)} → {m.Destination}"));
                StatusText = $"Preview: {moves.Count} proposed move(s). Review before starting.";
            }
            catch (Exception ex)
            {
                _previewRevision = -1;
                StatusText = "Preview failed · fix the folder rules before starting monitoring.";
                PreviewText = ex.Message;
            }
        }

        [RelayCommand]
        private async Task ToggleMonitoringAsync()
        {
            var wasRunning = App.Engine.IsRunning;
            try
            {
                if (wasRunning)
                {
                    App.Engine.Stop();
                    _settingsManager.GetSettings().MonitoringEnabled = false;
                    await _settingsManager.SaveSettingsAsync();
                    MonitoringButtonText = "Start monitoring";
                    StatusText = "Monitoring paused. Undo is available.";
                    InvalidatePreview("Monitoring paused. Run Preview again before restarting.");
                    return;
                }
                if (_previewRevision != _settingsManager.Revision)
                {
                    Preview();
                    if (_previewRevision == _settingsManager.Revision)
                        StatusText += " Press Start monitoring again after reviewing.";
                    return;
                }
                _settingsManager.GetSettings().MonitoringEnabled = true;
                await _settingsManager.SaveSettingsAsync();
                App.Engine.Start();
                MonitoringButtonText = "Pause monitoring";
                StatusText = "Monitoring approved folders.";
                InvalidatePreview("Monitoring is active. Pause before undo or reviewing a new plan.");
            }
            catch (Exception ex)
            {
                MonitoringButtonText = App.Engine.IsRunning ? "Pause monitoring" : "Start monitoring";
                if (!App.Engine.IsRunning && _settingsManager.GetCommittedSettings().MonitoringEnabled && !wasRunning)
                {
                    _settingsManager.GetSettings().MonitoringEnabled = false;
                    try { await _settingsManager.SaveSettingsAsync(); }
                    catch (Exception saveError)
                    {
                        StatusText = "Monitoring stayed paused, but auto-start may still be saved: " + saveError.Message;
                        return;
                    }
                }
                StatusText = wasRunning
                    ? "Monitoring paused now, but saving the pause failed. Auto-start may still be enabled: " + ex.Message
                    : "Monitoring was not started: " + ex.Message;
            }
        }

        [RelayCommand]
        private void UndoLast()
        {
            try
            {
                var undone = App.Engine.UndoLast();
                StatusText = "Restored " + Path.GetFileName(undone.Source) + ".";
                InvalidatePreview("A file was restored. Run Preview again before restarting.");
                RefreshRecent();
            }
            catch (Exception ex) { StatusText = "Undo refused: " + ex.Message; }
        }

        private void RefreshRecent()
        {
            try
            {
                var recent = App.Engine.RecentMoves();
                RecentMoveText = recent.Count == 0 ? "No recorded moves yet." :
                    string.Join(Environment.NewLine, recent.Take(5).Select(m =>
                        $"{m.Status}: {Path.GetFileName(m.Source)} → {m.Destination}"));
            }
            catch (Exception ex) { RecentMoveText = "Journal needs attention: " + ex.Message; }
        }

        private void InvalidatePreview(string message = "Rules changed. Run Preview again before starting.")
        {
            _previewRevision = -1;
            PreviewText = message;
        }
    }
}
