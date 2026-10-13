using System.Text.Json.Nodes;

namespace SmartFoldering.Tests;

public class RuleMatcherTests
{
    [Theory]
    [InlineData("pdf", "Report.PDF", true)]
    [InlineData(".pdf, docx", "notes.docx", true)]
    [InlineData("pdf docx;xlsx", "sheet.xlsx", true)]
    [InlineData("*.jpg", "photo.jpg", true)]
    [InlineData("tar.gz", "backup.tar.gz", true)]
    [InlineData("pdf", "notpdf", false)]
    [InlineData("pdf", ".pdf", false)]
    [InlineData("gz", "archive.tgz", false)]
    public void Extensions_match_case_insensitively_and_accept_lists(string value, string file, bool expected) =>
        Assert.Equal(expected, RuleMatcher.Matches(new Rule(RuleKind.Extension, value), file));

    [Theory]
    [InlineData("invoice", "Invoice-2026-03.pdf", true)]
    [InlineData("invoice", "summary.pdf", false)]
    [InlineData("pdf", "summary.pdf", false)]   // the extension is not part of the name
    public void Name_contains_ignores_case_and_the_extension(string value, string file, bool expected) =>
        Assert.Equal(expected, RuleMatcher.Matches(new Rule(RuleKind.NameContains, value), file));

    [Fact]
    public void Regular_expressions_work_and_invalid_ones_never_match()
    {
        Assert.True(RuleMatcher.Matches(new Rule(RuleKind.NameRegex, @"^IMG_\d{4}"), "img_2048.HEIC"));
        Assert.False(RuleMatcher.Matches(new Rule(RuleKind.NameRegex, @"^IMG_\d{4}"), "photo.jpg"));
        Assert.False(RuleMatcher.Matches(new Rule(RuleKind.NameRegex, "(unclosed"), "anything"));
        Assert.Equal("rule.error.regex", RuleMatcher.Validate(new Rule(RuleKind.NameRegex, "(unclosed")));
        Assert.Equal("rule.error.empty", RuleMatcher.Validate(new Rule(RuleKind.Extension, "  ")));
        Assert.Null(RuleMatcher.Validate(new Rule(RuleKind.Extension, "pdf")));
    }

    [Fact]
    public void The_first_destination_with_a_matching_rule_wins()
    {
        var folder = new WatchFolder
        {
            Destinations =
            {
                new Destination { Name = "Invoices", Path = "x", Rules = { new Rule(RuleKind.NameContains, "invoice") } },
                new Destination { Name = "Documents", Path = "y", Rules = { new Rule(RuleKind.Extension, "pdf") } }
            }
        };
        Assert.Equal("Invoices", RuleMatcher.FindDestination(folder, "invoice-42.pdf")!.Name);
        Assert.Equal("Documents", RuleMatcher.FindDestination(folder, "manual.pdf")!.Name);
        Assert.Null(RuleMatcher.FindDestination(folder, "song.mp3"));
    }

    [Fact]
    public void Templates_cover_common_downloads_without_overlaps()
    {
        var destinations = RuleTemplates.Create(@"C:\Users\me\Downloads");
        var all = destinations.SelectMany(d => RuleMatcher.Extensions(d.Rules[0].Value)).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal("Installers", RuleMatcher.FindDestination(new WatchFolder { Destinations = destinations }, "setup.exe")!.Name);
        Assert.EndsWith(Path.Combine("Downloads", "Images"), destinations.Single(d => d.Name == "Images").Path);
    }

    [Fact]
    public void Translated_templates_use_the_translated_folder_name()
    {
        var destinations = RuleTemplates.Create(@"C:\Users\me\Downloads", key => key == "documents" ? "Belgeler" : "");
        Assert.EndsWith(Path.Combine("Downloads", "Belgeler"), destinations[0].Path);
        Assert.Equal("Images", destinations[1].Name);   // an empty translation falls back to the English name
        Assert.Equal("music", RuleTemplates.For(new Rule(RuleKind.Extension, ".FLAC, wav"))!.Key);
        Assert.Null(RuleTemplates.For(new Rule(RuleKind.NameContains, "pdf")));
    }
}

public class FileSafetyTests
{
    [Theory]
    [InlineData("movie.mp4.crdownload", true)]
    [InlineData("big.iso.part", true)]
    [InlineData("~$report.docx", true)]
    [InlineData(".~lock.sheet.ods#", true)]
    [InlineData("desktop.ini", true)]
    [InlineData("report.docx", false)]
    [InlineData("partner.pdf", false)]
    public void Partial_downloads_and_lock_files_are_temporary(string name, bool expected) =>
        Assert.Equal(expected, FileSafety.IsTemporary(name));

    [Fact]
    public void An_empty_browser_placeholder_next_to_its_partial_download_is_left_alone()
    {
        using var tmp = new TempFolder();
        string placeholder = tmp.File("report.pdf", "", DateTime.UtcNow.AddHours(-1));
        tmp.File("report.pdf.part", "half");
        Assert.Equal(SkipReason.Placeholder, FileSafety.Inspect(new FileInfo(placeholder), DateTime.UtcNow));

        File.Delete(placeholder + ".part");
        Assert.Equal(SkipReason.None, FileSafety.Inspect(new FileInfo(placeholder), DateTime.UtcNow));

        string fresh = tmp.File("new.txt", "", DateTime.UtcNow);
        Assert.Equal(SkipReason.Placeholder, FileSafety.Inspect(new FileInfo(fresh), DateTime.UtcNow));
    }

    [Fact]
    public void A_file_open_for_writing_is_not_free()
    {
        using var tmp = new TempFolder();
        string path = tmp.File("busy.bin");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            Assert.False(FileSafety.IsFree(path));
        Assert.True(FileSafety.IsFree(path));
    }
}

public class SafeMoverTests
{
    [Fact]
    public void A_taken_name_becomes_numbered_and_nothing_is_overwritten()
    {
        using var tmp = new TempFolder();
        string target = tmp.Dir("Documents");
        tmp.File("Documents/cv.pdf", "old");
        string a = tmp.File("in/cv.pdf", "new one");
        string b = tmp.File("in2/cv.pdf", "newer");

        string? first = SafeMover.Move(a, target, ConflictPolicy.KeepBoth);
        string? second = SafeMover.Move(b, target, ConflictPolicy.KeepBoth);

        Assert.Equal(Path.Combine(target, "cv (2).pdf"), first);
        Assert.Equal(Path.Combine(target, "cv (3).pdf"), second);
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "cv.pdf")));
    }

    [Fact]
    public void Skip_leaves_the_file_where_it_is()
    {
        using var tmp = new TempFolder();
        string target = tmp.Dir("Documents");
        tmp.File("Documents/cv.pdf", "old");
        string source = tmp.File("in/cv.pdf", "new");

        Assert.Null(SafeMover.Move(source, target, ConflictPolicy.Skip));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void Missing_destination_folders_are_created()
    {
        using var tmp = new TempFolder();
        string source = tmp.File("in/a.txt");
        string target = Path.Combine(tmp.Root, "deep", "er", "folder");
        Assert.Equal(Path.Combine(target, "a.txt"), SafeMover.Move(source, target, ConflictPolicy.KeepBoth));
    }
}

public class SettingsTests
{
    [Fact]
    public void Settings_from_version_1_are_migrated()
    {
        var v1 = JsonNode.Parse("""
            {"WatchFolders":[{"Id":"a","Name":"Downloads","Path":"C:\\Users\\me\\Downloads\\","TargetFolders":[
              {"Id":"b","Name":"Docs","Path":"D:\\Docs","Rules":[{"Id":"r1","Type":0,"Value":".pdf"},{"Id":"r2","Type":1,"Value":"invoice"}]},
              {"Id":"c","Name":null,"Path":"D:\\Setup\\","Rules":[{"Type":0,"Value":".exe"}]}]}]}
            """)!.AsObject();

        var settings = SettingsStore.MigrateV1(v1);

        var folder = Assert.Single(settings.WatchFolders);
        Assert.Equal("Downloads", folder.Name);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(@"C:\Users\me\Downloads", folder.Path);              // the 1.x trailing separator is gone
            Assert.Equal(@"D:\Setup", folder.Destinations[1].Path);
        }
        Assert.Equal(2, folder.Destinations.Count);
        Assert.Equal(RuleKind.Extension, folder.Destinations[0].Rules[0].Kind);
        Assert.Equal(RuleKind.NameContains, folder.Destinations[0].Rules[1].Kind);
        Assert.Equal("Setup", folder.Destinations[1].Name);
    }

    [Fact]
    public void A_v1_file_on_disk_is_converted_and_saved_in_the_new_format()
    {
        using var tmp = new TempFolder();
        string path = tmp.File("settings.json", """{"WatchFolders":[{"Name":"D","Path":"C:\\D","TargetFolders":[]}]}""");

        var store = new SettingsStore(path);

        Assert.True(store.Migrated);
        Assert.Contains("\"Version\": 2", File.ReadAllText(path));
        Assert.False(new SettingsStore(path).Migrated);
    }

    [Fact]
    public void Snapshots_are_private_copies_and_updates_are_clamped_and_saved()
    {
        using var tmp = new TempFolder();
        string path = Path.Combine(tmp.Root, "settings.json");
        var store = new SettingsStore(path);
        int changes = 0;
        store.Changed += () => changes++;

        var snapshot = store.Snapshot();
        snapshot.QuietSeconds = 99;
        Assert.Equal(3, store.Snapshot().QuietSeconds);

        store.Update(s => { s.QuietSeconds = 999; s.Theme = "neon"; s.WatchFolders.Add(new WatchFolder { Name = "W", Path = tmp.Root }); });

        Assert.Equal(1, changes);
        var reloaded = new SettingsStore(path).Snapshot();
        Assert.Equal(AppSettings.MaxQuietSeconds, reloaded.QuietSeconds);
        Assert.Equal("system", reloaded.Theme);
        Assert.Single(reloaded.WatchFolders);
    }

    [Fact]
    public void An_unreadable_file_is_kept_aside_and_defaults_are_used()
    {
        using var tmp = new TempFolder();
        string path = tmp.File("settings.json", "{ this is not json");
        var store = new SettingsStore(path);
        Assert.Empty(store.Snapshot().WatchFolders);
        Assert.True(File.Exists(path + ".unreadable"));
    }
}
