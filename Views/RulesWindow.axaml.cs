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
    public partial class RulesWindow : Window
    {
        private WatchFolder _parentFolder;
        private TargetFolder _currentTarget;
        private SettingsManager _settingsManager;
        private Window _parentWindow;
        private ObservableCollection<Rule> _observableRules;

        public RulesWindow() { InitializeComponent(); }

        public RulesWindow(WatchFolder parentFolder, TargetFolder currentTarget, SettingsManager settingsManager, Window parentWindow) : this()
        {
            _parentFolder = parentFolder;
            _currentTarget = currentTarget;
            _settingsManager = settingsManager;
            _parentWindow = parentWindow;

            _observableRules = new ObservableCollection<Rule>(_currentTarget.Rules);
            this.DataContext = this;
        } 
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e); 
            if (_parentWindow != null)
            {
                this.Position = _parentWindow.Position;
            }
        }
        public ObservableCollection<Rule> ObservableRules => _observableRules;
        public string TargetName => _currentTarget?.Name ?? "Folder";

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                this.BeginMoveDrag(e);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _parentWindow.Show();
            this.Close();
        }

        private async void AddRule_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RuleDialogWindow();

        
            dialog.RuleValidator = (type, val) => App.GlobalSettings.CanAddRuleToTarget(_parentFolder.Id, type, val);

            await dialog.ShowDialog(this);

            if (dialog.IsSaved)
            {
                try
                {
                    var newRule = new Rule { Type = dialog.SelectedType, Value = dialog.RuleValue };
                    _observableRules.Add(newRule);
                    _currentTarget.Rules = _observableRules.ToList();
                    await App.GlobalSettings.SaveSettingsAsync();
                }
                catch (Exception ex) { RestoreRules(); new NotificationWindow("Rule not saved", ex.Message).Show(); }
            }
        }

        private async void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is Rule rule)
            {
                try
                {
                    _observableRules.Remove(rule);
                    _currentTarget.Rules = _observableRules.ToList();
                    await App.GlobalSettings.SaveSettingsAsync();
                }
                catch (Exception ex) { RestoreRules(); new NotificationWindow("Rule not saved", ex.Message).Show(); }
            }
        }

        private void RestoreRules()
        {
            _parentFolder = App.GlobalSettings.GetSettings().WatchFolders.First(x => x.Id == _parentFolder.Id);
            _currentTarget = _parentFolder.TargetFolders.First(x => x.Id == _currentTarget.Id);
            _observableRules.Clear();
            foreach (var rule in _currentTarget.Rules) _observableRules.Add(rule);
        }
    }
}
