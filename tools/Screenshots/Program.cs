// Renders the real windows with demo folders through Avalonia's headless Skia platform (no display needed), writes
// PNGs, and with --selftest drives the view models against real files:
//   dotnet run --project tools/Screenshots -- <output-dir> [--scale 2] [--matte] [--selftest]
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using SmartFoldering.Core;
using SmartFoldering.Localization;
using SmartFoldering.Services;
using SmartFoldering.ViewModels;
using SmartFoldering.Views;
using System.Diagnostics;
using System.Globalization;

namespace Screenshots;

internal static class Program
{
    private static string _outDir = "screenshots";
    private static double _scale = 1;
    private static bool _matte;
    private static DateTime _now = new(2026, 10, 12, 14, 30, 0, DateTimeKind.Utc);
    private static readonly BindingErrorListener BindingErrors = new();

    private static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        Trace.Listeners.Add(BindingErrors);
        _matte = args.Contains("--matte");
        int scaleArg = Array.IndexOf(args, "--scale");
        if (scaleArg >= 0 && scaleArg + 1 < args.Length) _scale = double.Parse(args[scaleArg + 1], CultureInfo.InvariantCulture);
        _outDir = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--") && !double.TryParse(a, CultureInfo.InvariantCulture, out _)) ?? "screenshots");
        Directory.CreateDirectory(_outDir);

        AppBuilder.Configure<SmartFoldering.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .LogToTrace(LogEventLevel.Warning, LogArea.Binding, LogArea.Property)
            .SetupWithoutStarting();

        string root = Path.Combine(Path.GetTempPath(), "smartfoldering-shots-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var demo = Demo.Create(root, () => _now);
            FriendlyPath.Home = demo.Home;   // "Ayla › Downloads", never this machine's temp folder
            CaptureAll(demo, Path.Combine(root, "empty-data"));
            FriendlyPath.Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (args.Contains("--selftest")) SelfTest(Path.Combine(root, "selftest"));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* temp folder */ }
        }

        if (BindingErrors.Errors.Count > 0)
        {
            Console.WriteLine($"{BindingErrors.Errors.Count} binding error(s):");
            foreach (string error in BindingErrors.Errors.Distinct().Take(20)) Console.WriteLine("  " + error);
            Environment.ExitCode = 1;
        }
        Console.WriteLine("Screenshots written to " + _outDir);
        return Environment.ExitCode;
    }

    // ------------------------------------------------------------------ screenshots

    private static void CaptureAll(Demo demo, string emptyDataDir)
    {
        var empty = new AppServices(emptyDataDir, () => _now);
        foreach (var (lang, theme) in new[] { ("en", ThemeVariant.Dark), ("en", ThemeVariant.Light), ("tr", ThemeVariant.Dark) })
        {
            string tag = $"{lang}-{(theme == ThemeVariant.Dark ? "dark" : "light")}";
            Loc.Instance.SetLanguage(lang);
            Application.Current!.RequestedThemeVariant = theme;

            var vm = new MainViewModel(demo.Services, () => _now);
            Capture($"main-{tag}", () => new MainWindow { DataContext = vm, Services = demo.Services });
            Capture($"welcome-{tag}", () => new MainWindow { DataContext = new MainViewModel(empty, () => _now), Services = empty });

            var downloads = demo.Services.Settings.Snapshot().WatchFolders[0];
            var invoices = downloads.Destinations.First(d => d.Rules[0].Kind == RuleKind.NameContains).Clone();
            invoices.Path = Path.Combine(@"C:\Users\Ayla", Path.GetRelativePath(demo.Home, invoices.Path));   // the text box shows the raw path
            var editor = new DestinationEditorViewModel(downloads, invoices) { TestName = "Invoice-2026-03.pdf" };
            Capture($"editor-{tag}", () => new DestinationWindow { DataContext = editor });

            var preview = new PreviewViewModel(downloads, demo.Services.Organizer.Preview(downloads));
            Capture($"preview-{tag}", () => new PreviewWindow { DataContext = preview });

            Capture($"settings-{tag}", () => new SettingsWindow { DataContext = new SettingsViewModel(demo.Services) });

            var toast = new ToastViewModel("Invoice-2026-03.pdf", Loc.Instance.Format("toast.one.body", invoices.Name), Look.ForFile("x.pdf"), "demo");
            Capture($"toast-{tag}", () => new ToastWindow { DataContext = toast });
        }
        Loc.Instance.SetLanguage("en");
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
    }

    private static void Capture(string name, Func<Window> create)
    {
        var window = create();
        // Scale before the first show: content moved after it was attached loses state (list selections reset).
        Size? scaled = _scale != 1 ? ScaleWindow(window) : null;
        window.Show();
        if (scaled is { } size)
        {
            // The dialogs shrink themselves to the (small) headless screen when they open; give them their size back.
            window.Width = size.Width;
            window.Height = size.Height;
        }
        if (window is MainWindow main) main.ShowFromTray();
        window.FindControl<Border>("Root")?.Classes.Set("hidden", false);
        Pump(0.9);

        using (WriteableBitmap? frame = window.CaptureRenderedFrame())
            frame?.Save(Path.Combine(_outDir, name + ".png"));

        if (_matte)
        {
            // The captured bitmap has no alpha. Rendering over black and over white lets tools/make_readme_images.py
            // recover the true transparency (rounded corners and shadows) with a difference matte.
            foreach (var (suffix, brush) in new[] { ("black", Brushes.Black), ("white", Brushes.White) })
            {
                window.Background = brush;
                Pump(0.15);
                using WriteableBitmap? matte = window.CaptureRenderedFrame();
                matte?.Save(Path.Combine(_outDir, $"{name}.{suffix}.png"));
            }
        }
        if (window is MainWindow closing) closing.CloseForShutdown();
        else window.Close();
        Console.WriteLine("  " + name);
    }

    /// <summary>
    /// Renders the window at a higher pixel density by scaling its layout (crisp output for documentation). Returns the
    /// new fixed size, or null for a window that sizes itself to its content.
    /// </summary>
    private static Size? ScaleWindow(Window window)
    {
        double width = window.Width, height = window.Height;
        var child = window.Content as Control;
        window.Content = null;
        Size? size = null;
        if (window.SizeToContent != SizeToContent.Manual)
        {
            if (child is not null) child.Width = width - 32;   // minus the root's 16 DIP margins
            window.SizeToContent = SizeToContent.WidthAndHeight;
        }
        else
        {
            size = new Size(width * _scale, height * _scale);
            window.Width = size.Value.Width;
            window.Height = size.Value.Height;
        }
        window.Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(_scale, _scale), Child = child };
        return size;
    }

    /// <summary>Lets timers, transitions and the renderer run for roughly the given time.</summary>
    private static void Pump(double seconds)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            Thread.Sleep(16);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        } while (sw.Elapsed.TotalSeconds < seconds);
    }

    // ------------------------------------------------------------------ self-test

    private static void SelfTest(string root)
    {
        void Check(bool ok, string what)
        {
            Console.WriteLine((ok ? "  PASS " : "  FAIL ") + what);
            if (!ok) Environment.ExitCode = 1;
        }

        Console.WriteLine("Self-test");
        Loc.Instance.SetLanguage("en");

        var missingTr = Strings.English.Keys.Except(Strings.Turkish.Keys).ToList();
        var missingEn = Strings.Turkish.Keys.Except(Strings.English.Keys).ToList();
        Check(missingTr.Count == 0 && missingEn.Count == 0, $"every string exists in both languages ({string.Join(", ", missingTr.Concat(missingEn))})");

        string home = Path.Combine(root, "Users", "ayla");
        FriendlyPath.Home = home;
        Check(FriendlyPath.Of(Path.Combine(home, "Downloads", "Documents")) == "ayla › Downloads › Documents"
              && FriendlyPath.Of(home + "2") == home + "2" && FriendlyPath.Of(Path.Combine(root, "Work")) == Path.Combine(root, "Work"),
              "paths in the profile read like the Explorer address bar, others stay as they are");
        FriendlyPath.Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string downloads = Directory.CreateDirectory(Path.Combine(root, "Downloads")).FullName;
        File.WriteAllText(Path.Combine(downloads, "manual.pdf"), "pdf");
        File.WriteAllText(Path.Combine(downloads, "invoice-0042.pdf"), "pdf");
        File.WriteAllText(Path.Combine(downloads, "holiday.jpg"), "jpg");
        File.WriteAllText(Path.Combine(downloads, "movie.mp4.crdownload"), "half");
        DateTime now = DateTime.UtcNow;
        var services = new AppServices(Path.Combine(root, "data"), () => now);
        var vm = new MainViewModel(services, () => now);
        var main = new MainWindow { DataContext = vm, Services = services };
        main.Show();
        Pump(0.2);

        // adding a folder
        Check(!vm.HasFolders && vm.StatusText == "No folders yet", "starts empty");
        Check(vm.AddFolder(downloads, withTemplates: true, "Downloads") is null, "Downloads can be added");
        Check(vm.HasFolders && vm.SelectedFolder?.Name == "Downloads" && vm.Destinations.Count == 7, "it is selected with the 7 ready-made destinations");
        Check(vm.SelectedFolder is { Enabled: false } && vm.ShowOffBanner, "a new folder starts switched off, with the preview banner");
        Check(vm.AddFolder(downloads + Path.DirectorySeparatorChar, true) == "folder.exists", "the same folder cannot be added twice");
        Check(vm.AddFolder(Path.Combine(downloads, "Documents"), true) == "folder.inside", "a destination cannot be watched as well");

        // preview
        var preview = vm.CreatePreview()!;
        Check(preview.Count == 3 && preview.ActionText == "Turn on sorting", $"preview lists the 3 finished files, not the partial download ({preview.Count})");
        Check(File.Exists(Path.Combine(downloads, "manual.pdf")), "preview moves nothing");

        // the destination editor
        var folder = vm.SelectedFolder!.Model;
        var editor = new DestinationEditorViewModel(folder, null);
        Check(!editor.Commit() && editor.ShowErrors && editor.ShownNameError is not null && editor.Rules[0].ShownError is not null,
              "saving an empty destination shows its errors");
        editor.Name = "Invoices";
        Check(editor.Path == Path.Combine(downloads, "Invoices"), "the folder follows the name");
        editor.Rules[0].KindIndex = 1;
        editor.Rules[0].Value = "invoice";
        editor.TestName = "invoice-0042.pdf";
        Check(!editor.TestMatches && editor.TestResult.Contains("Documents"), "the test box says Documents comes first and takes the file");
        editor.Rules.Add(new RuleRowViewModel(editor, new Rule(RuleKind.NameRegex, "(broken")));
        editor.Validate();
        Check(!editor.Commit() && editor.Rules[1].ShownError == "This is not a valid regular expression.", "an invalid regular expression blocks saving");
        editor.Rules[1].RemoveCommand.Execute(null);
        Check(editor.Commit() && editor.Result is { Name: "Invoices" }, "a valid destination saves");
        vm.SaveDestination(editor.Result!);
        var invoices = vm.Destinations.Last();
        for (int i = 0; i < 7; i++) vm.MoveUpCommand.Execute(vm.Destinations.First(d => d.Id == invoices.Id));
        Check(vm.Destinations[0].Name == "Invoices" && !vm.Destinations[0].CanMoveUp, "moving it to the top");
        var retest = new DestinationEditorViewModel(vm.SelectedFolder!.Model, vm.Destinations[0].Model) { TestName = "invoice-0042.pdf" };
        Check(retest.TestMatches, "now the invoice goes to Invoices");

        // sorting for real
        vm.StartSorting();
        Check(vm.SelectedFolder is { Enabled: true } && !vm.ShowOffBanner, "Turn on sorting switches the folder on");
        services.Organizer.ScanNow();
        now = now.AddSeconds(10);
        int moved = services.Organizer.ProcessDue();
        Pump(0.1);
        Check(moved == 3 && File.Exists(Path.Combine(downloads, "Invoices", "invoice-0042.pdf")) && File.Exists(Path.Combine(downloads, "Images", "holiday.jpg")),
              $"the engine sorts the files ({moved} moved)");
        Check(File.Exists(Path.Combine(downloads, "movie.mp4.crdownload")), "the partial download stays");
        vm.Tick();
        Check(vm.Activity.Count == 3 && vm.SortedToday == "3", "the activity list and the counter show the moves");

        var holiday = vm.Activity.First(a => a.FileName == "holiday.jpg");
        vm.UndoCommand.Execute(holiday);
        Check(File.Exists(Path.Combine(downloads, "holiday.jpg")) && vm.NoticeText is null, "Undo puts the file back");
        Check(vm.Activity.First(a => a.FileName == "holiday.jpg") is { IsUndone: true, CanUndo: false } && vm.SortedToday == "2", "the activity row shows it was put back");
        services.Organizer.ScanNow();
        now = now.AddSeconds(10);
        Check(services.Organizer.ProcessDue() == 0, "and the engine leaves it alone");

        // pause
        vm.TogglePauseCommand.Execute(null);
        Check(vm.IsPaused && vm.StatusText == "Paused" && !vm.IsActive, "pause");
        vm.TogglePauseCommand.Execute(null);
        Check(!vm.IsPaused && vm.StatusText == "Watching 1 folder", "resume");

        // settings write through the store and the language follows
        var settings = new SettingsViewModel(services);
        settings.LanguageIndex = 2;
        settings.QuietSeconds = 7;
        settings.ConflictIndex = 1;
        Loc.Instance.SetLanguage(services.Settings.Snapshot().Language);   // the app does this on Settings.Changed
        var saved = services.Settings.Snapshot();
        Check(saved.Language == "tr" && saved.QuietSeconds == 7 && saved.Conflict == ConflictPolicy.Skip, "settings are saved");
        Check(vm.StatusText == "1 klasör izleniyor" && settings.QuietText == "7 sn", $"switching to Turkish re-renders computed text ({vm.StatusText})");
        settings.Detach();
        Loc.Instance.SetLanguage("en");

        // removing a folder leaves its files alone
        vm.RemoveSelectedFolderCommand.Execute(null);
        Check(!vm.HasFolders && File.Exists(Path.Combine(downloads, "Invoices", "invoice-0042.pdf")), "stop watching keeps the files");

        main.CloseForShutdown();
        services.Dispose();
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && (message.Contains("[Binding]") || message.Contains("[Property]"))) Errors.Add(message);
        }
    }
}

/// <summary>Demo folders, rules and history that look like a real week of downloads.</summary>
internal sealed class Demo
{
    public required AppServices Services { get; init; }
    public required string Home { get; init; }

    public static Demo Create(string root, Func<DateTime> clock)
    {
        string home = Path.Combine(root, "Users", "Ayla");
        string downloads = Directory.CreateDirectory(Path.Combine(home, "Downloads")).FullName;
        string desktop = Directory.CreateDirectory(Path.Combine(home, "Desktop")).FullName;
        string camera = Directory.CreateDirectory(Path.Combine(home, "Pictures", "Camera Roll")).FullName;
        foreach (string file in new[] { "Invoice-2026-10.pdf", "quarterly-report.xlsx", "trip-to-lisbon.heic", "screen-recording.mp4", "dataset.zip",
                                        "VSCodeSetup-x64.exe", "podcast-episode-41.mp3", "thesis-draft.docx", "logo-final.svg", "big-download.iso.crdownload" })
            File.WriteAllText(Path.Combine(downloads, file), "demo");

        var services = new AppServices(Path.Combine(root, "data"), clock);
        services.Settings.Update(s =>
        {
            var templates = RuleTemplates.Create(downloads);
            templates.Insert(0, new Destination
            {
                Name = "Invoices",
                Path = Path.Combine(home, "Documents", "Invoices"),
                Rules = { new Rule(RuleKind.NameContains, "invoice"), new Rule(RuleKind.NameContains, "receipt") }
            });
            s.WatchFolders.Add(new WatchFolder { Name = "Downloads", Path = downloads, Destinations = templates });
            s.WatchFolders.Add(new WatchFolder
            {
                Name = "Desktop",
                Path = desktop,
                Destinations =
                {
                    new Destination { Name = "Screenshots", Path = Path.Combine(home, "Pictures", "Screenshots"), Rules = { new Rule(RuleKind.NameRegex, @"^Screenshot \d{4}-\d{2}-\d{2}") } },
                    new Destination { Name = "Notes", Path = Path.Combine(home, "Documents", "Notes"), Rules = { new Rule(RuleKind.Extension, "txt, md") } }
                }
            });
            s.WatchFolders.Add(new WatchFolder { Name = "Camera Roll", Path = camera, Enabled = false });
        });

        // A morning of moves; the clock is moved back and forth so the times look natural.
        var history = new (string File, string Destination, string Folder, int MinutesAgo)[]
        {
            ("Invoice-2026-09.pdf", "Invoices", "Documents\\Invoices", 3),
            ("Screenshot 2026-10-12 141502.png", "Screenshots", "Pictures\\Screenshots", 15),
            ("lecture-notes.md", "Notes", "Documents\\Notes", 41),
            ("ubuntu-26.04-desktop-amd64.iso", "Archives", "Downloads\\Archives", 64),
            ("boarding-pass.pdf", "Documents", "Downloads\\Documents", 98),
            ("team-photo.jpg", "Images", "Downloads\\Images", 143),
            ("GitHubDesktopSetup.exe", "Installers", "Downloads\\Installers", 190),
            ("budget-2027.xlsx", "Documents", "Downloads\\Documents", 260),
        };
        DateTime stamp = clock();
        var journal = new MoveJournal(Path.Combine(root, "data", "history.json"), () => stamp);
        string? teamPhoto = null;
        foreach (var (file, destination, folder, minutes) in history.Reverse())
        {
            string to = Path.Combine(home, folder, file);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.WriteAllText(to, "demo");
            stamp = clock().AddMinutes(-minutes);
            var record = journal.Record(Path.Combine(downloads, file), to, "demo", destination);
            if (file == "team-photo.jpg") teamPhoto = record.Id;
        }
        stamp = clock().AddMinutes(-120);
        journal.Undo(teamPhoto!);   // one of them was put back

        return new Demo { Services = new AppServices(Path.Combine(root, "data"), clock), Home = home };
    }
}
