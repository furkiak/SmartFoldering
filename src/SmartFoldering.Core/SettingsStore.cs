using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartFoldering.Core;

/// <summary>
/// The settings file (settings.json in the data folder). Readers get a private copy, writers go through
/// <see cref="Update"/>, which saves atomically and notifies listeners, so the UI and the background engine never
/// share a mutable object. Settings written by SmartFoldering 1.x are migrated on first load.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string? _path;
    private AppSettings _current;

    public event Action? Changed;

    /// <summary>True when the file came from SmartFoldering 1.x and was converted.</summary>
    public bool Migrated { get; }

    public SettingsStore(string? path)
    {
        _path = path;
        (_current, Migrated) = Load(path);
        if (Migrated) Save(_current);
    }

    /// <summary>A copy of the current settings; changing it has no effect until passed to <see cref="Update"/>.</summary>
    public AppSettings Snapshot()
    {
        lock (_gate) return _current.Clone();
    }

    public void Update(Action<AppSettings> edit)
    {
        lock (_gate)
        {
            var next = _current.Clone();
            edit(next);
            next.Normalize();
            _current = next;
            Save(next);
        }
        Changed?.Invoke();
    }

    private void Save(AppSettings settings)
    {
        if (_path is null) return;
        try
        {
            AtomicFile.Write(_path, JsonSerializer.Serialize(settings, Json));
        }
        catch (Exception ex)
        {
            Log.Error("Saving the settings failed", ex);
        }
    }

    private static (AppSettings, bool) Load(string? path)
    {
        if (path is null || !File.Exists(path)) return (new AppSettings(), false);
        try
        {
            string text = File.ReadAllText(path);
            var node = JsonNode.Parse(text);
            if (node is JsonObject obj && obj["Version"] is null && obj["WatchFolders"] is JsonArray)
                return (MigrateV1(obj), true);

            var settings = JsonSerializer.Deserialize<AppSettings>(text, Json) ?? new AppSettings();
            settings.Normalize();
            return (settings, false);
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            Log.Warn("settings.json is unreadable; keeping a copy and starting with defaults", ex);
            try { File.Copy(path, path + ".unreadable", overwrite: true); } catch { /* best effort */ }
            return (new AppSettings(), false);
        }
    }

    /// <summary>1.x: WatchFolders[{Name, Path, TargetFolders[{Name, Path, Rules[{Type: 0 extension | 1 keyword, Value}]}]}].</summary>
    internal static AppSettings MigrateV1(JsonObject v1)
    {
        var settings = new AppSettings();
        foreach (var folder in (v1["WatchFolders"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            string watchPath = Clean((string?)folder["Path"]);
            var watch = new WatchFolder
            {
                Name = (string?)folder["Name"] is { Length: > 0 } name ? name : Path.GetFileName(watchPath),
                Path = watchPath
            };
            foreach (var target in (folder["TargetFolders"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                string targetPath = Clean((string?)target["Path"]);
                var destination = new Destination
                {
                    Name = (string?)target["Name"] is { Length: > 0 } n ? n : Path.GetFileName(targetPath),
                    Path = targetPath
                };
                foreach (var rule in (target["Rules"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
                {
                    string value = (string?)rule["Value"] ?? "";
                    var kind = rule["Type"]?.GetValueKind() == JsonValueKind.Number && (int)rule["Type"]! == 1
                               || string.Equals(rule["Type"]?.ToString(), "Keyword", StringComparison.OrdinalIgnoreCase)
                        ? RuleKind.NameContains
                        : RuleKind.Extension;
                    if (value.Length > 0) destination.Rules.Add(new Rule(kind, value));
                }
                watch.Destinations.Add(destination);
            }
            if (watch.Path.Length > 0) settings.WatchFolders.Add(watch);
        }
        settings.Normalize();
        return settings;
    }

    /// <summary>1.x saved folders with a trailing separator ("C:\Users\me\Desktop\"); a drive root keeps its own.</summary>
    private static string Clean(string? path) => string.IsNullOrWhiteSpace(path) ? "" : Path.TrimEndingDirectorySeparator(path.Trim());
}
