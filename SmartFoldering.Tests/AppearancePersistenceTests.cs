using SmartFoldering.Models;
using SmartFoldering.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SmartFoldering.Tests;

public sealed class AppearancePersistenceTests
{
    [Fact]
    public async Task Legacy_defaults_upgrade_roundtrip_and_backup_preserve_operational_data()
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var legacy = JsonSerializer.SerializeToNode(Setup(root.Folder("source"), root.Folder("target")))!.AsObject();
        legacy.Remove(nameof(AppSettings.AppearanceTheme));
        legacy.Remove(nameof(AppSettings.TextScalePercent));
        legacy[nameof(AppSettings.MonitoringEnabled)] = true;
        var original = legacy.ToJsonString();
        File.WriteAllText(Path.Combine(folder, "settings.json"), original);
        var manager = new SettingsManager(folder);
        Assert.Equal("Dark", manager.GetCommittedSettings().AppearanceTheme);
        Assert.Equal(100, manager.GetCommittedSettings().TextScalePercent);
        var rules = JsonSerializer.Serialize(manager.GetCommittedSettings().WatchFolders);
        await manager.SaveAppearanceAsync("Light", 200);
        Assert.Equal(original, File.ReadAllText(Path.Combine(folder, "settings.json.bak")));
        manager = new SettingsManager(folder);
        Assert.Equal("Light", manager.GetCommittedSettings().AppearanceTheme);
        Assert.Equal(200, manager.GetCommittedSettings().TextScalePercent);
        Assert.True(manager.GetCommittedSettings().MonitoringEnabled);
        Assert.Equal(rules, JsonSerializer.Serialize(manager.GetCommittedSettings().WatchFolders));
        await manager.SaveAppearanceAsync("Dark", 150);
        manager = new SettingsManager(folder);
        Assert.Equal("Dark", manager.GetCommittedSettings().AppearanceTheme);
        Assert.Equal(150, manager.GetCommittedSettings().TextScalePercent);
        Assert.Equal(rules, JsonSerializer.Serialize(manager.GetCommittedSettings().WatchFolders));
    }

    [Fact]
    public async Task Unknown_top_level_settings_are_preserved_during_appearance_and_rule_saves()
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var node = JsonSerializer.SerializeToNode(new AppSettings())!.AsObject();
        node["FuturePreference"] = JsonNode.Parse("{\"enabled\":true,\"labels\":[\"one\",\"two\"]}");
        File.WriteAllText(Path.Combine(folder, "settings.json"), node.ToJsonString());
        var manager = new SettingsManager(folder);
        await manager.SaveAppearanceAsync("Light", 200);
        await manager.SaveSettingsAsync();
        var saved = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "settings.json")))!;
        Assert.True(JsonNode.DeepEquals(node["FuturePreference"], saved["FuturePreference"]));
    }

    [Fact]
    public async Task Appearance_keeps_approved_revision_and_running_watcher_delivers_a_later_file()
    {
        using var root = new OwnedRoot();
        var source = root.Folder("source");
        var target = root.Folder("target");
        var manager = new SettingsManager(root.Folder("settings"));
        manager.GetSettings().WatchFolders = Setup(source, target).WatchFolders;
        await manager.SaveSettingsAsync();
        manager.GetSettings().MonitoringEnabled = true;
        await manager.SaveSettingsAsync();
        using var engine = new FileOrganizerEngine(manager, new MoveJournal(root.Folder("journal")));
        var changedEvents = 0;
        manager.RulesChanging += () => { changedEvents++; engine.Stop(); };
        var moved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.OnScanCompleted += count => moved.TrySetResult(count);
        engine.Start();
        var revision = manager.Revision;
        await manager.SaveAppearanceAsync("Light", 200);
        await manager.SaveAppearanceAsync("Dark", 150);
        Assert.Equal(revision, manager.Revision);
        Assert.Equal(0, changedEvents);
        Assert.True(engine.IsRunning);
        Assert.True(manager.GetCommittedSettings().MonitoringEnabled);
        File.WriteAllText(Path.Combine(source, "after-theme.pdf"), "unchanged payload");
        Assert.Equal(1, await moved.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal("unchanged payload", File.ReadAllText(Path.Combine(target, "after-theme.pdf")));
        engine.Stop();
    }

    [Fact]
    public async Task Appearance_does_not_commit_pending_rule_edits_or_replace_bound_current_objects()
    {
        using var root = new OwnedRoot();
        var manager = new SettingsManager(root.Folder("settings"));
        manager.GetSettings().WatchFolders = Setup(root.Folder("source"), root.Folder("target")).WatchFolders;
        await manager.SaveSettingsAsync();
        var boundFolder = manager.GetSettings().WatchFolders.Single();
        boundFolder.TargetFolders[0].Rules.Add(new Rule { Type = RuleType.Extension, Value = ".txt" });
        var revision = manager.Revision;
        var changedEvents = 0;
        manager.RulesChanging += () => changedEvents++;
        await manager.SaveAppearanceAsync("Light", 200);
        Assert.Same(boundFolder, manager.GetSettings().WatchFolders.Single());
        Assert.Single(manager.GetCommittedSettings().WatchFolders[0].TargetFolders[0].Rules);
        Assert.Equal(2, boundFolder.TargetFolders[0].Rules.Count);
        Assert.Equal(revision, manager.Revision);
        Assert.Equal(0, changedEvents);
        await manager.SaveSettingsAsync();
        Assert.Equal(1, changedEvents);
        Assert.Equal(revision + 1, manager.Revision);
        Assert.Equal(2, manager.GetCommittedSettings().WatchFolders[0].TargetFolders[0].Rules.Count);
        Assert.Equal("Light", manager.GetCommittedSettings().AppearanceTheme);
    }

    [Fact]
    public async Task Failed_appearance_save_keeps_main_backup_preferences_and_revision()
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var manager = new SettingsManager(folder);
        await manager.SaveSettingsAsync();
        await manager.SaveAppearanceAsync("Light", 150);
        var path = Path.Combine(folder, "settings.json");
        var main = File.ReadAllBytes(path);
        var backup = File.ReadAllBytes(path + ".bak");
        var revision = manager.Revision;
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => manager.SaveAppearanceAsync("Dark", 200));
        Assert.Equal(main, File.ReadAllBytes(path));
        Assert.Equal(backup, File.ReadAllBytes(path + ".bak"));
        Assert.Equal(revision, manager.Revision);
        Assert.Equal("Light", manager.GetSettings().AppearanceTheme);
        Assert.Equal(150, manager.GetCommittedSettings().TextScalePercent);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    [Fact]
    public async Task Concurrent_appearance_saves_keep_valid_backup_and_do_not_change_preview_revision()
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var manager = new SettingsManager(folder);
        await manager.SaveSettingsAsync();
        var revision = manager.Revision;
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            manager.SaveAppearanceAsync(i % 2 == 0 ? "Light" : "Dark", i % 3 == 0 ? 200 : 150)));
        var reloaded = new SettingsManager(folder).GetCommittedSettings();
        Assert.Equal(manager.GetCommittedSettings().AppearanceTheme, reloaded.AppearanceTheme);
        Assert.Equal(manager.GetCommittedSettings().TextScalePercent, reloaded.TextScalePercent);
        Assert.Equal(revision, manager.Revision);
        using var backup = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "settings.json.bak")));
        Assert.Equal(JsonValueKind.Object, backup.RootElement.ValueKind);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    [Theory]
    [InlineData("Automatic", 100)]
    [InlineData("Light", 125)]
    [InlineData("", 200)]
    [InlineData(null, 100)]
    public async Task Invalid_appearance_request_does_not_write_or_change_any_committed_state(string? theme, int scale)
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var manager = new SettingsManager(folder);
        await manager.SaveSettingsAsync();
        var path = Path.Combine(folder, "settings.json");
        var before = File.ReadAllBytes(path);
        await Assert.ThrowsAsync<ArgumentException>(() => manager.SaveAppearanceAsync(theme!, scale));
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.Equal("Dark", manager.GetSettings().AppearanceTheme);
        Assert.Equal(100, manager.GetCommittedSettings().TextScalePercent);
        Assert.Equal(1, manager.Revision);
    }

    [Theory]
    [InlineData("\"AppearanceTheme\":null")]
    [InlineData("\"AppearanceTheme\":1")]
    [InlineData("\"AppearanceTheme\":\"System\"")]
    [InlineData("\"TextScalePercent\":\"200\"")]
    [InlineData("\"TextScalePercent\":125")]
    [InlineData("\"AppearanceTheme\":\"Light\",\"AppearanceTheme\":\"Dark\"")]
    public void Unsupported_preferences_preserve_main_and_older_backup_without_silent_recovery(string preference)
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var path = Path.Combine(folder, "settings.json");
        var original = "{\"WatchFolders\":[],\"MonitoringEnabled\":false," + preference + "}";
        var backup = JsonSerializer.Serialize(new AppSettings());
        File.WriteAllText(path, original);
        File.WriteAllText(path + ".bak", backup);
        Assert.Throws<InvalidDataException>(() => new SettingsManager(folder));
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Equal(backup, File.ReadAllText(path + ".bak"));
        Assert.Empty(Directory.GetFiles(folder, "*.corrupt-*"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task External_incompatible_main_or_backup_blocks_appearance_save_without_overwriting_either(bool backup)
    {
        using var root = new OwnedRoot();
        var folder = root.Folder("settings");
        var manager = new SettingsManager(folder);
        await manager.SaveSettingsAsync();
        await manager.SaveSettingsAsync();
        var path = Path.Combine(folder, "settings.json");
        File.WriteAllText(backup ? path + ".bak" : path, "{\"WatchFolders\":[],\"TextScalePercent\":300}");
        var main = File.ReadAllBytes(path);
        var previous = File.ReadAllBytes(path + ".bak");
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.SaveAppearanceAsync("Light", 200));
        Assert.Equal(main, File.ReadAllBytes(path));
        Assert.Equal(previous, File.ReadAllBytes(path + ".bak"));
        Assert.Equal("Dark", manager.GetCommittedSettings().AppearanceTheme);
        Assert.Equal(2, manager.Revision);
        Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }

    private static AppSettings Setup(string source, string target) => new()
    {
        WatchFolders = [new WatchFolder
        {
            Name = "Source", Path = source, TargetFolders = [new TargetFolder
            {
                Name = "Target", Path = target, Rules = [new Rule { Type = RuleType.Extension, Value = ".pdf" }]
            }]
        }]
    };

    private sealed class OwnedRoot : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "SmartFoldering-appearance-" + Guid.NewGuid().ToString("N"));
        public OwnedRoot() => Directory.CreateDirectory(_path);
        public string Folder(string name) => Directory.CreateDirectory(Path.Combine(_path, name)).FullName;
        public void Dispose() => Directory.Delete(_path, recursive: true);
    }
}
