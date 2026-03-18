using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;

namespace SmartFoldering
{
    public partial class FolderDialogWindow : Window
    {
        public string FolderName { get; private set; }
        public string FolderPath { get; private set; }
        public bool IsSaved { get; private set; } = false;


        public FolderDialogWindow() { InitializeComponent(); }
         
        public FolderDialogWindow(string existingName, string existingPath) : this()
        {
            NameTextBox.Text = existingName;
            PathTextBox.Text = existingPath;
        }

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                this.BeginMoveDrag(e);
            }
        }

        private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder",
                    AllowMultiple = false
                });

                if (folders != null && folders.Count > 0)
                {
                    PathTextBox.Text = folders[0].Path.LocalPath;
                }
            }
            catch (Exception) {   }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text) || string.IsNullOrWhiteSpace(PathTextBox.Text)) return;
            FolderName = NameTextBox.Text.Trim();
            FolderPath = PathTextBox.Text.Trim();
            IsSaved = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsSaved = false;
            this.Close();
        }
    }
}