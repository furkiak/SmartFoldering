using SmartFoldering.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartFoldering.Services;

public interface ISettingsManager
{
    AppSettings GetSettings();
    AppSettings GetCommittedSettings();
    int Revision { get; }
    Task SaveSettingsAsync();
    bool CanAddRuleToTarget(string watchFolderId, RuleType type, string value);
}

public sealed class SettingsManager : ISettingsManager
{
    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private AppSettings _currentSettings;
    private AppSettings _committedSettings;
    private int _revision;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public event Action? RulesChanging;

    public int Revision => Volatile.Read(ref _revision);

    public SettingsManager(string? dataFolder = null)
    {
        var directory = dataFolder ?? AppStorage.Profile.Directory;
        Directory.CreateDirectory(directory);
        _settingsFilePath = Path.Combine(directory, "settings.json");
        _currentSettings = ReadOrRecover();
        _committedSettings = Clone(_currentSettings);
    }

    public AppSettings GetSettings() => _currentSettings;
    public AppSettings GetCommittedSettings() => Clone(_committedSettings);

    public async Task SaveSettingsAsync()
    {
        await _writeGate.WaitAsync();
        try
        {
            var rulesChanged = JsonSerializer.Serialize(_currentSettings.WatchFolders) !=
                               JsonSerializer.Serialize(_committedSettings.WatchFolders);
            if (rulesChanged)
            {
                RulesChanging?.Invoke();
                _currentSettings.MonitoringEnabled = false;
            }
            MovePlanner.Validate(_currentSettings);
            var json = JsonSerializer.Serialize(_currentSettings, JsonOptions);
            var path = _settingsFilePath;
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    await stream.WriteAsync(bytes);
                    stream.Flush(flushToDisk: true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            _committedSettings = Clone(_currentSettings);
            Interlocked.Increment(ref _revision);
        }
        catch
        {
            _currentSettings = Clone(_committedSettings);
            throw;
        }
        finally { _writeGate.Release(); }
    }

    public bool CanAddRuleToTarget(string watchFolderId, RuleType type, string value)
    {
        var watch = _currentSettings.WatchFolders.FirstOrDefault(x => x.Id == watchFolderId);
        return watch != null && !watch.TargetFolders.SelectMany(x => x.Rules)
            .Any(rule => rule.Type == type && rule.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
    }

    private AppSettings ReadOrRecover()
    {
        if (!File.Exists(_settingsFilePath))
        {
            if (!File.Exists(_settingsFilePath + ".bak")) return new AppSettings();
            var backup = Read(_settingsFilePath + ".bak");
            File.Copy(_settingsFilePath + ".bak", _settingsFilePath);
            return backup;
        }
        try { return Read(_settingsFilePath); }
        catch (JsonException)
        {
            AppSettings backup;
            try { backup = Read(_settingsFilePath + ".bak"); }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                throw new InvalidDataException("Settings are damaged; originals were preserved. Restore settings.json or its backup before running.", ex);
            }
            File.Move(_settingsFilePath, _settingsFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
            File.Copy(_settingsFilePath + ".bak", _settingsFilePath);
            return backup;
        }
    }

    private static AppSettings Read(string path) =>
        JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ??
        throw new JsonException("Settings must contain an object.");

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
}
