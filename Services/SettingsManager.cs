using Serilog;
using SmartFoldering.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartFoldering.Services
{
    public interface ISettingsManager
    {
        AppSettings GetSettings();
        Task SaveSettingsAsync();
        bool CanAddRuleToTarget(string watchFolderId, RuleType type, string value);
    }

    public class SettingsManager : ISettingsManager
    {
        private readonly string _settingsFilePath;
        private readonly string _settingsDirectory;
        private AppSettings _currentSettings;

        public SettingsManager()
        {
            _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartFoldering");
            _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

            if (!Directory.Exists(_settingsDirectory))
            {
                Directory.CreateDirectory(_settingsDirectory);
            }
             
            LoadSettingsSync();
        }

        public AppSettings GetSettings() => _currentSettings;
         
        private void LoadSettingsSync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _currentSettings = new AppSettings();
                    SaveSettingsSync();  
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Setting load error"); 
                _currentSettings = new AppSettings();
            }
        }

        private void SaveSettingsSync()
        {
            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
         
        public async Task SaveSettingsAsync()
        {
            try
            { 
                string tempPath = _settingsFilePath + ".tmp";

                string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempPath, json);

                File.Move(tempPath, _settingsFilePath, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving settings");  
            }
        }

        public bool CanAddRuleToTarget(string watchFolderId, RuleType type, string value)
        {
            var watchFolder = _currentSettings.WatchFolders.FirstOrDefault(w => w.Id == watchFolderId);
            if (watchFolder == null) return false;

            foreach (var target in watchFolder.TargetFolders)
            {
                bool exists = target.Rules.Any(r => r.Type == type && r.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
                if (exists) return false;
            }
            return true;
        }
    }
}