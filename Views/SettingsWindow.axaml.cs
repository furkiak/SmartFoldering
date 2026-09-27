using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System;
using System.IO;
using SmartFoldering.Views;

namespace SmartFoldering
{
    public partial class SettingsWindow : Window
    {
        private const string AppName = "SmartFoldering";
        private bool _initialAutoStart;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                this.BeginMoveDrag(e);
        }

        private void LoadSettings()
        {
            var settings = App.GlobalSettings.GetCommittedSettings();
            ThemeChoice.SelectedIndex = settings.AppearanceTheme == "Light" ? 0 : 1;
            TextSizeChoice.SelectedIndex = settings.TextScalePercent switch { 150 => 1, 200 => 2, _ => 0 };
            if (Services.AppStorage.Profile.IsSeparate)
            {
                StartupCheckBox.IsEnabled = false;
                StartupDescription.Text = "This profile keeps its files separate and does not change your normal Windows startup entry.";
                return;
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null)
                {
                    var val = key.GetValue(AppName); 
                    StartupCheckBox.IsChecked = val != null;
                }
                _initialAutoStart = StartupCheckBox.IsChecked == true;
            }
            else
            { 
                StartupCheckBox.IsEnabled = false;
                StartupCheckBox.Content = "Startup (Windows Only)";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;
            FeedbackText.IsVisible = false;
            var appearanceSaved = false;
            try
            {
                if (ThemeChoice.SelectedIndex is not (0 or 1) || TextSizeChoice.SelectedIndex is not (0 or 1 or 2))
                    throw new InvalidOperationException("Choose a theme and text size before saving.");
                await App.GlobalSettings.SaveAppearanceAsync(ThemeChoice.SelectedIndex == 0 ? "Light" : "Dark",
                    TextSizeChoice.SelectedIndex switch { 1 => 150, 2 => 200, _ => 100 });
                appearanceSaved = true;
                Services.AppearanceService.Apply(App.GlobalSettings.GetCommittedSettings());
                if (OperatingSystem.IsWindows() && !Services.AppStorage.Profile.IsSeparate &&
                    (StartupCheckBox.IsChecked == true) != _initialAutoStart)
                {
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)
                        ?? throw new InvalidOperationException("Could not open Windows startup settings.");
                    if (StartupCheckBox.IsChecked == true)
                    {
                        string exePath = Path.Combine(AppContext.BaseDirectory, "SmartFoldering.exe");
                        if (!File.Exists(exePath)) throw new FileNotFoundException("The published SmartFoldering.exe is required for auto-start.", exePath);
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else key.DeleteValue(AppName, false);
                }
                this.Close();
            }
            catch (Exception ex)
            {
                FeedbackText.Text = (appearanceSaved ? "Appearance saved; Windows startup was not saved. " : "Settings not saved. ") + ex.Message;
                FeedbackText.IsVisible = true;
            }
            finally
            {
                SaveButton.IsEnabled = true;
                if (FeedbackText.IsVisible) SaveButton.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
