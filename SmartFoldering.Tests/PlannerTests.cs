using SmartFoldering.Models;
using SmartFoldering.Services;

namespace SmartFoldering.Tests;

public class PlannerTests
{
    [Fact]
    public void Rejects_same_folder_case_variant_and_descendant()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var settings = Setup(source, source);
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
        settings.WatchFolders[0].TargetFolders[0].Path = source.ToUpperInvariant();
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
        settings.WatchFolders[0].TargetFolders[0].Path = Path.Combine(source, "sorted");
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
    }

    [Fact]
    public void Rejects_two_way_cycle_and_overlapping_watched_roots()
    {
        using var root = new TestRoot();
        var a = root.Folder("A");
        var b = root.Folder("B");
        var settings = Setup(a, b);
        settings.WatchFolders.Add(Setup(b, a).WatchFolders[0]);
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
        settings.WatchFolders[1].Path = Path.Combine(a, "nested");
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
    }

    [Fact]
    public void Preview_reserves_collision_names_and_matches_actual_safe_moves()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        File.WriteAllText(Path.Combine(source, "a.pdf"), "first");
        File.WriteAllText(Path.Combine(target, "a.pdf"), "existing");
        var settings = Setup(source, target);
        var planned = MovePlanner.Preview(settings);
        Assert.Single(planned);
        Assert.EndsWith("a (1).pdf", planned[0].Destination);
        Assert.True(MovePlanner.IsStillCurrent(planned[0]));
        File.Move(planned[0].Source, planned[0].Destination);
        Assert.Equal("first", File.ReadAllText(planned[0].Destination));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(target, "a.pdf")));
        Assert.Empty(MovePlanner.Preview(settings));
    }

    [Fact]
    public void Changed_file_is_rejected_before_move()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        var file = Path.Combine(source, "a.pdf");
        File.WriteAllText(file, "first");
        var planned = MovePlanner.Preview(Setup(source, target)).Single();
        File.AppendAllText(file, " changed");
        Assert.False(MovePlanner.IsStillCurrent(planned));
    }

    [Fact]
    public void Rejects_empty_keyword_that_would_match_every_file()
    {
        using var root = new TestRoot();
        var settings = Setup(root.Folder("Downloads"), root.Folder("Sorted"));
        settings.WatchFolders[0].TargetFolders[0].Rules[0].Type = RuleType.Keyword;
        settings.WatchFolders[0].TargetFolders[0].Rules[0].Value = "";
        Assert.Throws<InvalidOperationException>(() => MovePlanner.Validate(settings));
    }

    [Fact]
    public void Journal_moves_and_undoes_only_its_original_file()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        File.WriteAllText(Path.Combine(source, "a.pdf"), "my report");
        var plan = MovePlanner.Preview(Setup(source, target)).Single();
        var journal = new MoveJournal(root.Folder("journal"));
        var moved = journal.Move(plan);
        Assert.Equal("moved", moved.Status);
        Assert.False(File.Exists(plan.Source));
        Assert.Equal("my report", File.ReadAllText(plan.Destination));
        var undone = journal.UndoLast();
        Assert.Equal("undone", undone.Status);
        Assert.Equal("my report", File.ReadAllText(plan.Source));
        Assert.False(File.Exists(plan.Destination));
    }

    [Fact]
    public void Undo_refuses_modified_destination_and_existing_source()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        File.WriteAllText(Path.Combine(source, "a.pdf"), "original");
        var plan = MovePlanner.Preview(Setup(source, target)).Single();
        var journal = new MoveJournal(root.Folder("journal"));
        journal.Move(plan);
        File.AppendAllText(plan.Destination, " edited");
        Assert.Throws<IOException>(() => journal.UndoLast());
        Assert.False(File.Exists(plan.Source));
        File.WriteAllText(plan.Source, "new file");
        Assert.Throws<IOException>(() => journal.UndoLast());
        Assert.Equal("new file", File.ReadAllText(plan.Source));
    }

    [Fact]
    public void Unmoved_intent_does_not_block_undo_of_previous_completed_move()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        File.WriteAllText(Path.Combine(source, "a.pdf"), "first");
        var plan = MovePlanner.Preview(Setup(source, target)).Single();
        var journalFolder = root.Folder("journal");
        var journal = new MoveJournal(journalFolder);
        journal.Move(plan);
        var stalled = new MoveEvent(Guid.NewGuid(), "intent", Path.Combine(source, "later.pdf"),
            Path.Combine(target, "later.pdf"), new string('A', 64), DateTime.UtcNow.AddMinutes(1));
        File.WriteAllText(stalled.Source, "not moved");
        File.AppendAllText(Path.Combine(journalFolder, "operations.jsonl"),
            System.Text.Json.JsonSerializer.Serialize(stalled) + Environment.NewLine);
        Assert.Equal("undone", journal.UndoLast().Status);
        Assert.True(File.Exists(plan.Source));
        Assert.True(File.Exists(stalled.Source));
    }

    [Fact]
    public void Damaged_journal_blocks_a_new_move_without_touching_source()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        File.WriteAllText(Path.Combine(source, "a.pdf"), "first");
        var plan = MovePlanner.Preview(Setup(source, target)).Single();
        var journalFolder = root.Folder("journal");
        File.WriteAllText(Path.Combine(journalFolder, "operations.jsonl"), "{}" + Environment.NewLine);
        Assert.Throws<InvalidDataException>(() => new MoveJournal(journalFolder).Move(plan));
        Assert.True(File.Exists(plan.Source));
        Assert.False(File.Exists(plan.Destination));
    }

    [Fact]
    public async Task Single_worker_runs_initial_scan_and_stop_blocks_later_moves()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        var settings = new SettingsManager(root.Folder("settings"));
        settings.GetSettings().WatchFolders = Setup(source, target).WatchFolders;
        await settings.SaveSettingsAsync();
        File.WriteAllText(Path.Combine(source, "a.pdf"), "first");
        using var engine = new FileOrganizerEngine(settings, new MoveJournal(root.Folder("journal")));
        var completed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.OnScanCompleted += count => completed.TrySetResult(count);
        engine.Start();
        Assert.True(engine.IsRunning);
        Assert.Equal(1, await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        engine.Stop();
        Assert.False(engine.IsRunning);
        Assert.Equal("first", File.ReadAllText(Path.Combine(target, "a.pdf")));
        File.WriteAllText(Path.Combine(source, "b.pdf"), "second");
        await Task.Delay(250);
        Assert.True(File.Exists(Path.Combine(source, "b.pdf")));
    }

    [Fact]
    public async Task Stop_cancels_locked_file_wait_without_moving_it()
    {
        using var root = new TestRoot();
        var source = root.Folder("Downloads");
        var target = root.Folder("Sorted");
        var settings = new SettingsManager(root.Folder("settings"));
        settings.GetSettings().WatchFolders = Setup(source, target).WatchFolders;
        await settings.SaveSettingsAsync();
        var file = Path.Combine(source, "locked.pdf");
        File.WriteAllText(file, "unfinished");
        using var lockHandle = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var engine = new FileOrganizerEngine(settings, new MoveJournal(root.Folder("journal")));
        engine.Start();
        await Task.Delay(100);
        engine.Stop();
        Assert.True(File.Exists(file));
        Assert.False(File.Exists(Path.Combine(target, "locked.pdf")));
    }

    [Fact]
    public async Task Concurrent_settings_saves_use_unique_temporary_files_and_keep_valid_json()
    {
        using var root = new TestRoot();
        var settings = new SettingsManager(root.Folder("settings"));
        settings.GetSettings().WatchFolders = Setup(root.Folder("Downloads"), root.Folder("Sorted")).WatchFolders;
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => settings.SaveSettingsAsync()));
        Assert.Equal(20, settings.Revision);
        var reloaded = new SettingsManager(System.IO.Path.Combine(root.Path, "settings"));
        Assert.Single(reloaded.GetCommittedSettings().WatchFolders);
        Assert.Empty(Directory.GetFiles(System.IO.Path.Combine(root.Path, "settings"), "*.tmp"));
    }

    [Fact]
    public async Task Failed_settings_write_keeps_last_committed_configuration()
    {
        using var root = new TestRoot();
        var folder = root.Folder("settings");
        var settings = new SettingsManager(folder);
        settings.GetSettings().WatchFolders = Setup(root.Folder("Downloads"), root.Folder("Sorted")).WatchFolders;
        await settings.SaveSettingsAsync();
        var file = System.IO.Path.Combine(folder, "settings.json");
        var before = File.ReadAllText(file);
        settings.GetSettings().WatchFolders[0].Name = "Modified";
        using (var lockHandle = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => settings.SaveSettingsAsync());
        Assert.Equal(before, File.ReadAllText(file));
        Assert.Equal("Downloads", settings.GetCommittedSettings().WatchFolders[0].Name);
        Assert.Equal("Downloads", settings.GetSettings().WatchFolders[0].Name);
    }

    [Fact]
    public async Task Changing_rules_pauses_monitoring_before_commit()
    {
        using var root = new TestRoot();
        var settings = new SettingsManager(root.Folder("settings"));
        settings.GetSettings().WatchFolders = Setup(root.Folder("Downloads"), root.Folder("Sorted")).WatchFolders;
        await settings.SaveSettingsAsync();
        settings.GetSettings().MonitoringEnabled = true;
        await settings.SaveSettingsAsync();
        using var engine = new FileOrganizerEngine(settings, new MoveJournal(root.Folder("journal")));
        settings.RulesChanging += engine.Stop;
        engine.Start();
        settings.GetSettings().WatchFolders[0].TargetFolders[0].Rules.Add(new Rule { Type = RuleType.Extension, Value = ".txt" });
        await settings.SaveSettingsAsync();
        Assert.False(engine.IsRunning);
        Assert.False(settings.GetCommittedSettings().MonitoringEnabled);
    }

    private static AppSettings Setup(string source, string target) => new()
    {
        WatchFolders = [new WatchFolder
        {
            Name = "Downloads", Path = source,
            TargetFolders = [new TargetFolder
            {
                Name = "Sorted", Path = target,
                Rules = [new Rule { Type = RuleType.Extension, Value = ".pdf" }]
            }]
        }]
    };

    private sealed class TestRoot : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmartFoldering-tests-" + Guid.NewGuid().ToString("N"));
        public TestRoot() => Directory.CreateDirectory(Path);
        public string Folder(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
