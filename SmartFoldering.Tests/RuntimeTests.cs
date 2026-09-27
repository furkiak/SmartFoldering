using SmartFoldering.Models;
using SmartFoldering.Services;

namespace SmartFoldering.Tests;

public class RuntimeTests
{
    [Fact]
    public void Separate_profile_canonical_path_has_its_own_stable_instance_lock()
    {
        var path = Path.Combine(Path.GetTempPath(), "SmartFoldering-profile-" + Guid.NewGuid().ToString("N"));
        var a = AppStorage.Resolve(["--data-directory", path]);
        var b = AppStorage.Resolve(["--data-directory", Path.Combine(path, "child", "..")]);
        Assert.True(a.IsSeparate);
        Assert.Equal(a.Directory, b.Directory);
        Assert.Equal(a.MutexName, b.MutexName);
        Assert.NotEqual(AppStorage.Resolve([]).MutexName, a.MutexName);
        Assert.NotEqual(a.MutexName, AppStorage.Resolve(["--data-directory", path + "-other"]).MutexName);
    }

    [Theory]
    [InlineData("--data-directory", "relative")]
    [InlineData("--data-directory", "")]
    [InlineData("--unknown", "C:\\profile")]
    public void Invalid_profile_option_cannot_silently_use_the_personal_settings(string option, string path)
    {
        Assert.Throws<ArgumentException>(() => AppStorage.Resolve([option, path]));
    }

    [Fact]
    public async Task Watcher_and_repeated_scans_move_a_large_file_once_and_preserve_rename_and_stop()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartFoldering-runtime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(root, "target")).FullName;
            var settings = new SettingsManager(Path.Combine(root, "profile"));
            settings.GetSettings().WatchFolders.Add(new WatchFolder
            {
                Name = "Disposable files", Path = source,
                TargetFolders = [new TargetFolder
                {
                    Name = "Output", Path = target,
                    Rules = [new Rule { Type = RuleType.Extension, Value = ".pdf" }]
                }]
            });
            await settings.SaveSettingsAsync();
            using var engine = new FileOrganizerEngine(settings, new MoveJournal(Path.Combine(root, "profile")));
            var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
            engine.OnError += errors.Enqueue;
            engine.Start();
            var download = Path.Combine(source, "large.part");
            var ready = Path.Combine(source, "large.pdf");
            await using (var writer = new FileStream(download, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var block = Enumerable.Repeat((byte)0x5A, 1024 * 1024).ToArray();
                for (var i = 0; i < 64; i++) await writer.WriteAsync(block);
                writer.Flush(flushToDisk: true);
                for (var i = 0; i < 20; i++) engine.ForceScanNow();
                Assert.False(File.Exists(Path.Combine(target, "large.pdf")));
            }
            File.Move(download, ready);
            for (var i = 0; i < 20; i++) engine.ForceScanNow();
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(Path.Combine(target, "large.pdf")) && DateTime.UtcNow < until)
                await Task.Delay(50);
            engine.Stop();
            Assert.False(File.Exists(ready));
            Assert.Equal(64L * 1024 * 1024, new FileInfo(Path.Combine(target, "large.pdf")).Length);
            Assert.Single(engine.RecentMoves());
            Assert.Equal("moved", engine.RecentMoves()[0].Status);
            Assert.Empty(errors);
            var restored = engine.UndoLast();
            Assert.Equal("undone", restored.Status);
            Assert.Equal(64L * 1024 * 1024, new FileInfo(ready).Length);
            Assert.False(File.Exists(Path.Combine(target, "large.pdf")));
            await File.WriteAllTextAsync(Path.Combine(source, "after-stop.pdf"), "stay here");
            engine.ForceScanNow();
            await Task.Delay(100);
            Assert.True(File.Exists(Path.Combine(source, "after-stop.pdf")));
            Assert.False(File.Exists(Path.Combine(target, "after-stop.pdf")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
