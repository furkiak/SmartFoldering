using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmartFoldering.Models;
using SmartFoldering.Services;
using SmartFoldering.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SmartFoldering
{
    public partial class TargetFoldersWindow : Window
    {
        private WatchFolder _currentFolder = new();
        private SettingsManager? _settingsManager;
         
        private ObservableCollection<TargetFolder> _observableTargets = new();
        private Window? _parentWindow;
        public TargetFoldersWindow()
        {
            InitializeComponent();
        }
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e); 
            if (_parentWindow != null)
            {
                this.Position = _parentWindow.Position;
            }
        }
        public TargetFoldersWindow(WatchFolder selectedFolder, SettingsManager settingsManager) : this()
        {
            _currentFolder = selectedFolder;
            _settingsManager = settingsManager;
             
            _observableTargets = new ObservableCollection<TargetFolder>(_currentFolder.TargetFolders);
             
            this.DataContext = this;
        }
         
        public ObservableCollection<TargetFolder> ObservableTargets => _observableTargets;
        public string FolderName => _currentFolder?.Name ?? "Unknown Folder";
         
        private async void AddTargetFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderDialogWindow();
            await dialog.ShowDialog(this);

            if (dialog.IsSaved)
            {
                try
                {
                    var newTarget = new TargetFolder { Name = dialog.FolderName, Path = dialog.FolderPath };
                    _observableTargets.Add(newTarget);
                    _currentFolder.TargetFolders = _observableTargets.ToList();
                    await App.GlobalSettings.SaveSettingsAsync();
                }
                catch (Exception ex) { RestoreTargets(); new NotificationWindow("Destination not saved", ex.Message).Show(); }
            }
        }
         
        private async void DeleteTarget_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is TargetFolder target)
            {
                try
                {
                    _observableTargets.Remove(target);
                    _currentFolder.TargetFolders = _observableTargets.ToList();
                    await App.GlobalSettings.SaveSettingsAsync();
                }
                catch (Exception ex) { RestoreTargets(); new NotificationWindow("Destination not saved", ex.Message).Show(); }
            }
        }
         
        private async void EditTarget_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is TargetFolder target)
            {
                var dialog = new FolderDialogWindow(target.Name, target.Path);
                await dialog.ShowDialog(this);

                if (dialog.IsSaved)
                {
                    try
                    {
                        target.Name = dialog.FolderName;
                        target.Path = dialog.FolderPath;
                        _currentFolder.TargetFolders = _observableTargets.ToList();
                        await App.GlobalSettings.SaveSettingsAsync();
                        var index = _observableTargets.IndexOf(target);
                        if (index >= 0) _observableTargets[index] = target;
                    }
                    catch (Exception ex) { RestoreTargets(); new NotificationWindow("Destination not saved", ex.Message).Show(); }
                }
            }
        }
          
        public TargetFoldersWindow(WatchFolder selectedFolder, SettingsManager settingsManager, Window parentWindow) : this()
        {
            _currentFolder = selectedFolder;
            _settingsManager = settingsManager;
            _parentWindow = parentWindow;  

            _observableTargets = new ObservableCollection<TargetFolder>(_currentFolder.TargetFolders);
            this.DataContext = this;
        }
         
        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }
         
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow?.Show();
            this.Close();  
        } 
        private void OpenRules_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is TargetFolder target)
            {
                var rulesWindow = new RulesWindow(_currentFolder, target, _settingsManager ?? App.GlobalSettings, this);
                this.Hide();
                rulesWindow.Show();
            }
        }

        private void RestoreTargets()
        {
            _currentFolder = App.GlobalSettings.GetSettings().WatchFolders.First(x => x.Id == _currentFolder.Id);
            _observableTargets.Clear();
            foreach (var target in _currentFolder.TargetFolders) _observableTargets.Add(target);
        }
    }
}
