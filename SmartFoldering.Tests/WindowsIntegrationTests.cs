using System.Collections.Concurrent;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using SmartFoldering.Models;
using SmartFoldering.Services;
using Xunit.Abstractions;

namespace SmartFoldering.Tests;

public sealed class WindowsIntegrationFactAttribute : FactAttribute
{
    public WindowsIntegrationFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("SMARTFOLDERING_WINDOWS_TEST_ROOT")))
            Skip = "Requires Windows and an explicitly chosen disposable SMARTFOLDERING_WINDOWS_TEST_ROOT.";
    }
}

public sealed class WindowsIntegrationTests(ITestOutputHelper output)
{
    [WindowsIntegrationFact]
    public async Task Real_kernel_overflow_rescans_files_whose_notifications_were_lost_during_an_active_scan()
    {
        using var root = new OwnedRoot();
        var source = root.Folder("source");
        var target = root.Folder("target");
        var settings = await Configure(root, source, target);
        var lockedPath = Path.Combine(source, "00-locked.pdf");
        File.WriteAllText(lockedPath, "original locked file");
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using var engine = new FileOrganizerEngine(settings, new MoveJournal(root.Folder("journal")));
        var errors = new ConcurrentQueue<string>();
        engine.OnError += errors.Enqueue;
        engine.Start();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var queueGate = typeof(FileOrganizerEngine).GetField("_gate", flags)!.GetValue(engine)!;
        var watchers = (List<FileSystemWatcher>)typeof(FileOrganizerEngine).GetField("_watchers", flags)!.GetValue(engine)!;
        var watcher = Assert.Single(watchers);
        Assert.Equal(64 * 1024, watcher.InternalBufferSize);
        var nativeErrors = new ConcurrentQueue<Exception>();
        var received = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        watcher.Error += (_, e) => nativeErrors.Enqueue(e.GetException());
        watcher.Created += (_, e) => received.TryAdd(e.FullPath, 0);
        watcher.Changed += (_, e) => received.TryAdd(e.FullPath, 0);
        watcher.Renamed += (_, e) => received.TryAdd(e.FullPath, 0);
        // The initial full scan has captured the exclusively locked file before this burst.
        await Task.Delay(250);
        var sentinels = Enumerable.Range(0, 3).Select(i => Path.Combine(source, $"lost-{i}.pdf")).ToArray();
        var expectedHashes = new Dictionary<string, string>();
        // Hold the real queue gate so the native watcher callback cannot finish/rearm immediately.
        // No event or exception is synthesized: Windows must raise the overflow itself.
        lock (queueGate)
        {
            var blocker = Path.Combine(source, "blocker.tmp");
            File.WriteAllText(blocker, "temporary");
            Thread.Sleep(150);
            for (var i = 0; i < 1200; i++)
            {
                var noise = Path.Combine(source, $"{new string('n', 100)}-{i:D4}.tmp");
                File.WriteAllText(noise, "temporary");
                File.Delete(noise);
            }
            foreach (var path in sentinels)
            {
                File.WriteAllText(path, "sentinel " + Path.GetFileName(path));
                expectedHashes[path] = Hash(path);
            }
            File.Delete(blocker);
        }
        await WaitUntil(() => nativeErrors.Any(e => e is InternalBufferOverflowException), 10,
            "Windows did not produce an actual InternalBufferOverflowException; this is not overflow evidence.");
        var lost = sentinels.Where(path => !received.ContainsKey(path)).ToArray();
        Assert.NotEmpty(lost);
        Assert.Contains(errors, e => e.Contains("full scan was queued", StringComparison.Ordinal));
        output.WriteLine($"Native overflow: {nativeErrors.First(e => e is InternalBufferOverflowException).GetType().FullName}; buffer={watcher.InternalBufferSize}; notification burst=1200 create/delete; lost sentinel events={lost.Length}");
        locked.Dispose();
        await WaitUntil(() => sentinels.All(path => File.Exists(Path.Combine(target, Path.GetFileName(path)))), 10,
            "A full scan requested during an active scan did not recover the dropped sentinel notifications.");
        engine.Stop();
        foreach (var path in sentinels)
        {
            Assert.False(File.Exists(path));
            Assert.Equal(expectedHashes[path], Hash(Path.Combine(target, Path.GetFileName(path))));
        }
        Assert.Equal(4, engine.RecentMoves().Count);
        Assert.All(engine.RecentMoves(), move => Assert.Equal("moved", move.Status));
        Assert.Equal("original locked file", File.ReadAllText(Path.Combine(target, "00-locked.pdf")));
        output.WriteLine("Recovered four files exactly once with unchanged SHA256; no fallback timer or ForceScanNow was used.");
    }

    [WindowsIntegrationFact]
    public async Task Real_directory_acl_denial_preserves_last_settings_and_owner_then_recovers()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var root = new OwnedRoot();
        var settings = await Configure(root, root.Folder("source"), root.Folder("target"));
        var directory = new DirectoryInfo(root.Folder("settings"));
        var access = directory.GetAccessControl(AccessControlSections.Access);
        var originalRules = AccessRuleFingerprint(access);
        var originalDacl = access.GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        var restore = new DirectorySecurity();
        restore.SetSecurityDescriptorSddlForm(originalDacl, AccessControlSections.Access);
        var owner = directory.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
        var sid = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Windows user SID unavailable.");
        var denied = directory.GetAccessControl(AccessControlSections.Access);
        denied.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.CreateFiles,
            InheritanceFlags.None, PropagationFlags.None, AccessControlType.Deny));
        var file = Path.Combine(directory.FullName, "settings.json");
        var original = File.ReadAllBytes(file);
        var backup = File.ReadAllBytes(file + ".bak");
        var revision = settings.Revision;
        try
        {
            directory.SetAccessControl(denied);
            var probe = Assert.Throws<UnauthorizedAccessException>(() =>
                File.WriteAllText(Path.Combine(directory.FullName, "denied-probe.tmp"), "must be denied"));
            Assert.Equal(5, probe.HResult & 0xFFFF);
            settings.GetSettings().WatchFolders[0].Name = "Uncommitted ACL change";
            var failure = await Assert.ThrowsAsync<UnauthorizedAccessException>(settings.SaveSettingsAsync);
            Assert.Equal(5, failure.HResult & 0xFFFF);
            AssertSettingsPreserved(settings, file, original, backup, revision);
            output.WriteLine($"Actual current-user directory CreateFiles deny: {failure.GetType().Name}, HResult=0x{failure.HResult:X8}; main/backup/revision/current/committed preserved.");
        }
        finally { directory.SetAccessControl(restore); }
        Assert.True(Equals(owner, directory.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier))),
            "The test must not change directory ownership.");
        var restoredAccess = directory.GetAccessControl(AccessControlSections.Access);
        Assert.Equal(originalRules, AccessRuleFingerprint(restoredAccess));
        Assert.Equal(access.AreAccessRulesProtected, restoredAccess.AreAccessRulesProtected);
        settings.GetSettings().WatchFolders[0].Name = "Saved after ACL restoration";
        await settings.SaveSettingsAsync();
        Assert.Equal(revision + 1, settings.Revision);
        Assert.Equal("Saved after ACL restoration", new SettingsManager(directory.FullName).GetCommittedSettings().WatchFolders[0].Name);
        output.WriteLine("Original DACL and owner restored; next atomic save succeeded.");
    }

    [WindowsIntegrationFact]
    public async Task Real_readonly_file_denial_keeps_main_backup_and_committed_state()
    {
        using var root = new OwnedRoot();
        var settings = await Configure(root, root.Folder("source"), root.Folder("target"));
        var file = Path.Combine(root.Folder("settings"), "settings.json");
        var attributes = File.GetAttributes(file);
        var original = File.ReadAllBytes(file);
        var backup = File.ReadAllBytes(file + ".bak");
        var revision = settings.Revision;
        try
        {
            File.SetAttributes(file, attributes | FileAttributes.ReadOnly);
            settings.GetSettings().WatchFolders[0].Name = "Uncommitted readonly change";
            var failure = await Record.ExceptionAsync(settings.SaveSettingsAsync);
            Assert.NotNull(failure);
            Assert.True(failure is UnauthorizedAccessException or IOException, failure.ToString());
            AssertSettingsPreserved(settings, file, original, backup, revision);
            output.WriteLine($"Actual readonly destination replacement denial: {failure.GetType().Name}, HResult=0x{failure.HResult:X8}; main/backup/revision/current/committed preserved.");
        }
        finally { File.SetAttributes(file, attributes); }
        await settings.SaveSettingsAsync();
        Assert.Equal(revision + 1, settings.Revision);
    }

    private static void AssertSettingsPreserved(SettingsManager settings, string file, byte[] original, byte[] backup, int revision)
    {
        Assert.Equal(original, File.ReadAllBytes(file));
        Assert.Equal(backup, File.ReadAllBytes(file + ".bak"));
        Assert.Equal(revision, settings.Revision);
        Assert.Equal("Disposable files", settings.GetSettings().WatchFolders[0].Name);
        Assert.Equal("Disposable files", settings.GetCommittedSettings().WatchFolders[0].Name);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp"));
    }

    private static async Task<SettingsManager> Configure(OwnedRoot root, string source, string target)
    {
        var settings = new SettingsManager(root.Folder("settings"));
        settings.GetSettings().WatchFolders = [new WatchFolder
        {
            Name = "Disposable files", Path = source,
            TargetFolders = [new TargetFolder
            {
                Name = "Output", Path = target,
                Rules = [new Rule { Type = RuleType.Extension, Value = ".pdf" }]
            }]
        }];
        await settings.SaveSettingsAsync();
        await settings.SaveSettingsAsync();
        return settings;
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string AccessRuleFingerprint(DirectorySecurity security)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var rules = new List<string>();
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            rules.Add($"{rule.IdentityReference.Value}|{rule.AccessControlType}|{rule.FileSystemRights}|{rule.InheritanceFlags}|{rule.PropagationFlags}|{rule.IsInherited}");
        rules.Sort(StringComparer.Ordinal);
        // Windows may normalize the DACL's auto-inherited flag; compare every ACE and inheritance protection instead.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rules))));
    }

    private static async Task WaitUntil(Func<bool> predicate, int seconds, string failure)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!predicate() && DateTime.UtcNow < until) await Task.Delay(25);
        Assert.True(predicate(), failure);
    }

    private sealed class OwnedRoot : IDisposable
    {
        private readonly string _parent;
        private readonly string _ownerToken = Guid.NewGuid().ToString("N");
        public string Path { get; }
        public OwnedRoot()
        {
            _parent = Environment.GetEnvironmentVariable("SMARTFOLDERING_WINDOWS_TEST_ROOT")
                ?? throw new InvalidOperationException("Choose an explicit disposable integration root.");
            if (!System.IO.Path.IsPathFullyQualified(_parent)) throw new InvalidOperationException("Integration root must be absolute.");
            _parent = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(_parent));
            MovePlanner.EnsureNoLinks(_parent);
            Path = System.IO.Path.Combine(_parent, "owned-" + _ownerToken);
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, ".smartfoldering-test-owner"), _ownerToken);
        }
        public string Folder(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;
        public void Dispose()
        {
            if (!Path.StartsWith(_parent + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                File.ReadAllText(System.IO.Path.Combine(Path, ".smartfoldering-test-owner")) != _ownerToken)
                throw new InvalidOperationException("Refuse cleanup outside this test's owned fixture.");
            MovePlanner.EnsureNoLinks(Path);
            Directory.Delete(Path, recursive: true);
        }
    }
}
