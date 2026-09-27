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
            ValidateAppearance(_currentSettings.AppearanceTheme, _currentSettings.TextScalePercent);
            var rulesChanged = JsonSerializer.Serialize(_currentSettings.WatchFolders) !=
                               JsonSerializer.Serialize(_committedSettings.WatchFolders);
            if (rulesChanged)
            {
                RulesChanging?.Invoke();
                _currentSettings.MonitoringEnabled = false;
            }
            MovePlanner.Validate(_currentSettings);
            await PersistAsync(_currentSettings);
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

    public async Task SaveAppearanceAsync(string theme, int textScalePercent)
    {
        ValidateAppearance(theme, textScalePercent);
        await _writeGate.WaitAsync();
        try
        {
            var candidate = Clone(_committedSettings);
            candidate.AppearanceTheme = theme;
            candidate.TextScalePercent = textScalePercent;
            await PersistAsync(candidate);
            _committedSettings = candidate;
            _currentSettings.AppearanceTheme = theme;
            _currentSettings.TextScalePercent = textScalePercent;
        }
        finally { _writeGate.Release(); }
    }

    public static void ValidateAppearance(string theme, int textScalePercent)
    {
        if (theme is not ("Light" or "Dark") || textScalePercent is not (100 or 150 or 200))
            throw new ArgumentException("Appearance must be Light or Dark with 100%, 150% or 200% text size.");
    }

    private async Task PersistAsync(AppSettings settings)
    {
        // Refuse incompatible external edits before replacing either original or backup.
        if (File.Exists(_settingsFilePath)) _ = Read(_settingsFilePath);
        if (File.Exists(_settingsFilePath + ".bak")) _ = Read(_settingsFilePath + ".bak");
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temporary = _settingsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_settingsFilePath)) File.Replace(temporary, _settingsFilePath, _settingsFilePath + ".bak");
            else File.Move(temporary, _settingsFilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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

    private static AppSettings Read(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("Settings must contain an object.");
        var theme = "Dark";
        var scale = 100;
        var seenTheme = false;
        var seenScale = false;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name == nameof(AppSettings.AppearanceTheme))
            {
                if (seenTheme || property.Value.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Unsupported appearance data; original settings were preserved.");
                seenTheme = true;
                theme = property.Value.GetString()!;
            }
            if (property.Name == nameof(AppSettings.TextScalePercent))
            {
                if (seenScale || property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out scale))
                    throw new InvalidDataException("Unsupported text size data; original settings were preserved.");
                seenScale = true;
            }
        }
        try { ValidateAppearance(theme, scale); }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("Unsupported appearance preference; original settings were preserved. Use a compatible version or explicitly restore your backup.", ex);
        }
        return JsonSerializer.Deserialize<AppSettings>(json) ?? throw new JsonException("Settings must contain an object.");
    }

    private static AppSettings Clone(AppSettings settings) =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
}
