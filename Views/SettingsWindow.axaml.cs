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
            if (Services.AppStorage.Profile.IsSeparate)
            {
                StartupCheckBox.IsEnabled = false;
                StartupCheckBox.Content = "Windows sign-in startup is unavailable for a separate profile";
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
            }
            else
            { 
                StartupCheckBox.IsEnabled = false;
                StartupCheckBox.Content = "Startup (Windows Only)";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (OperatingSystem.IsWindows() && !Services.AppStorage.Profile.IsSeparate)
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
            catch (Exception ex) { new NotificationWindow("Settings not saved", ex.Message).Show(); }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
