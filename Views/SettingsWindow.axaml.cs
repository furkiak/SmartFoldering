using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null)
                {
                    if (StartupCheckBox.IsChecked == true)
                    { 
                        string exePath = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(AppName, $"\"{exePath}\"");
                    }
                    else
                    { 
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}