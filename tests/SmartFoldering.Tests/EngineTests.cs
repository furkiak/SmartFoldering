namespace SmartFoldering.Tests;

/// <summary>The engine against real folders, with a hand-driven clock (no timers, no waiting).</summary>
public sealed class OrganizerTests : IDisposable
{
    private readonly TempFolder _tmp = new();
    private readonly TestClock _clock = new();
    private readonly string _downloads;
    private readonly SettingsStore _settings;
    private readonly MoveJournal _journal;
    private readonly Organizer _organizer;

    public OrganizerTests()
    {
        _downloads = _tmp.Dir("Downloads");
        _settings = new SettingsStore(null);
        _settings.Update(s => s.WatchFolders.Add(new WatchFolder
        {
            Id = "downloads",
            Name = "Downloads",
            Path = _downloads,
            Destinations = RuleTemplates.Create(_downloads)
        }));
        _journal = new MoveJournal(Path.Combine(_tmp.Root, "history.json"), _clock.Func);
        _organizer = new Organizer(_settings, _journal, _clock.Func);
    }

    public void Dispose()
    {
        _organizer.Dispose();
        _tmp.Dispose();
    }

    private int Settle()
    {
        _clock.Advance(TimeSpan.FromSeconds(10));
        return _organizer.ProcessDue();
    }

    [Fact]
    public void Files_are_sorted_after_the_quiet_period()
    {
        string pdf = _tmp.File("Downloads/manual.pdf");
        string song = _tmp.File("Downloads/song.mp3");
        string unknown = _tmp.File("Downloads/notes.xyz");
        _organizer.ScanNow();

        Assert.Equal(0, _organizer.ProcessDue());   // not quiet yet
        Assert.Equal(2, Settle());

        Assert.True(File.Exists(Path.Combine(_downloads, "Documents", "manual.pdf")));
        Assert.True(File.Exists(Path.Combine(_downloads, "Music", "song.mp3")));
        Assert.True(File.Exists(unknown));
        Assert.False(File.Exists(pdf) || File.Exists(song));
        Assert.Equal(2, _journal.Recent().Count);
    }

    [Fact]
    public void A_file_that_is_still_growing_waits()
    {
        string path = _tmp.File("Downloads/video.mp4", "first part");
        _organizer.Enqueue(path, "downloads");

        File.AppendAllText(path, " and more");   // the download continues
        Assert.Equal(0, Settle());
        Assert.True(File.Exists(path));
        Assert.Equal(1, _organizer.WaitingCount);

        Assert.Equal(1, Settle());                 // now it stayed the same for a whole quiet period
        Assert.True(File.Exists(Path.Combine(_downloads, "Videos", "video.mp4")));
        Assert.Equal(0, _organizer.WaitingCount);
    }

    [Fact]
    public void Turning_a_folder_on_queues_the_files_already_in_it()
    {
        _settings.Update(s => s.WatchFolders[0].Enabled = false);
        _tmp.File("Downloads/old.pdf");
        _organizer.Start();

        Assert.Equal(0, _organizer.PendingCount);
        _settings.Update(s => s.Theme = "dark");       // unrelated change: nothing happens
        Assert.Equal(0, _organizer.PendingCount);
        _settings.Update(s => s.WatchFolders[0].Enabled = true);
        Assert.Equal(1, _organizer.PendingCount);
        Assert.Equal(0, _organizer.WaitingCount);       // queued, not busy
    }

    [Fact]
    public void A_file_held_open_by_another_program_waits_until_it_is_released()
    {
        string path = _tmp.File("Downloads/locked.zip");
        _organizer.Enqueue(path, "downloads");
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            Assert.Equal(0, Settle());
        Assert.Equal(1, Settle());
    }

    [Fact]
    public void Partial_downloads_and_their_placeholders_are_never_moved()
    {
        string part = _tmp.File("Downloads/movie.mp4.crdownload", "half a movie");
        string placeholder = _tmp.File("Downloads/report.pdf", "", DateTime.UtcNow.AddHours(-1));
        _tmp.File("Downloads/report.pdf.part", "half a report");
        _organizer.ScanNow();

        Assert.Equal(0, Settle());
        Assert.True(File.Exists(part));
        Assert.True(File.Exists(placeholder));

        // the browser finishes: the partial file is renamed to its real name
        File.Delete(placeholder);
        File.Delete(placeholder + ".part");
        File.Move(part, Path.Combine(_downloads, "movie.mp4"));
        _organizer.Enqueue(Path.Combine(_downloads, "movie.mp4"), "downloads");
        Assert.Equal(1, Settle());
    }

    [Fact]
    public void Undo_puts_the_file_back_and_the_engine_leaves_it_there()
    {
        string path = _tmp.File("Downloads/contract.docx");
        _organizer.ScanNow();
        Assert.Equal(1, Settle());
        var record = _journal.Recent()[0];

        Assert.Equal(UndoResult.Undone, _journal.Undo(record.Id));
        Assert.True(File.Exists(path));
        Assert.Equal(UndoResult.AlreadyUndone, _journal.Undo(record.Id));

        _organizer.ScanNow();
        Assert.Equal(0, Settle());                         // held: not sorted straight back
        Assert.True(File.Exists(path));

        _clock.Advance(MoveJournal.HoldTime);
        _organizer.ScanNow();
        Assert.Equal(1, Settle());                         // a day later it is sorted again
    }

    [Fact]
    public void Paused_engine_moves_nothing_until_resumed()
    {
        _tmp.File("Downloads/a.png");
        _settings.Update(s => s.Paused = true);
        _organizer.ScanNow();
        Assert.Equal(0, Settle());

        _settings.Update(s => s.Paused = false);
        Assert.Equal(1, Settle());
    }

    [Fact]
    public void Preview_lists_what_would_move_without_moving_it()
    {
        _tmp.File("Downloads/a.pdf");
        _tmp.File("Downloads/b.zip");
        _tmp.File("Downloads/c.part");
        _tmp.File("Downloads/d.unknown");

        var plan = _organizer.Preview(_settings.Snapshot().WatchFolders[0]);

        Assert.Equal(new[] { "a.pdf", "b.zip" }, plan.Select(p => Path.GetFileName(p.File)));
        Assert.Equal(new[] { "Documents", "Archives" }, plan.Select(p => p.Destination.Name));
        Assert.True(File.Exists(Path.Combine(_downloads, "a.pdf")));
    }

    [Fact]
    public void A_destination_that_is_the_watched_folder_itself_is_ignored()
    {
        _settings.Update(s => s.WatchFolders[0].Destinations.Insert(0, new Destination
        {
            Name = "Same", Path = _downloads, Rules = { new Rule(RuleKind.Extension, "txt") }
        }));
        string path = _tmp.File("Downloads/loop.txt");
        _organizer.ScanNow();
        Assert.Equal(0, Settle());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void The_history_survives_a_restart()
    {
        _tmp.File("Downloads/x.mp3");
        _organizer.ScanNow();
        Settle();

        var reopened = new MoveJournal(Path.Combine(_tmp.Root, "history.json"), _clock.Func);
        Assert.Equal("x.mp3", Assert.Single(reopened.Recent()).FileName);
        Assert.Equal(1, reopened.CountSince(DateTime.UtcNow.AddHours(-1)));
    }

    [Fact]
    public async Task The_real_watcher_and_loop_sort_a_new_file()
    {
        var live = new Organizer(_settings, new MoveJournal(null), () => DateTime.UtcNow);
        _settings.Update(s => s.QuietSeconds = 1);
        live.Start();
        try
        {
            string path = Path.Combine(_downloads, "live.pdf");
            await File.WriteAllTextAsync(path, "hello");
            string expected = Path.Combine(_downloads, "Documents", "live.pdf");
            for (int i = 0; i < 100 && !File.Exists(expected); i++) await Task.Delay(100);
            Assert.True(File.Exists(expected));
        }
        finally
        {
            live.Dispose();
        }
    }
}
