using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SmartFoldering.Models;
using System;

namespace SmartFoldering
{
    public partial class RuleDialogWindow : Window
    {
        public RuleType SelectedType { get; private set; }
        public string RuleValue { get; private set; }
        public bool IsSaved { get; private set; } = false;
         
        public Func<RuleType, string, bool> RuleValidator { get; set; }

        public RuleDialogWindow() { InitializeComponent(); }

        private void Window_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                this.BeginMoveDrag(e);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ValueTextBox.Text)) return;

            SelectedType = ExtensionRadio.IsChecked == true ? RuleType.Extension : RuleType.Keyword;
            string val = ValueTextBox.Text.Trim();

            if (SelectedType == RuleType.Extension && !val.StartsWith("."))
                val = "." + val;

            val = val.ToLower();
             
            if (RuleValidator != null && !RuleValidator(SelectedType, val))
            {
                var errorBox = this.FindControl<Border>("ErrorBox");
                if (errorBox != null) errorBox.IsVisible = true;
                return;
            }

            RuleValue = val;
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