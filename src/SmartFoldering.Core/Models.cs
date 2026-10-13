using System.Text.Json.Serialization;

namespace SmartFoldering.Core;

/// <summary>How a rule recognises a file.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RuleKind>))]
public enum RuleKind
{
    /// <summary>One or more extensions, for example <c>pdf, docx</c>.</summary>
    Extension,

    /// <summary>The file name contains a word, for example <c>invoice</c>.</summary>
    NameContains,

    /// <summary>The file name matches a regular expression.</summary>
    NameRegex
}

/// <summary>What happens when a file with the same name is already in the destination.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ConflictPolicy>))]
public enum ConflictPolicy
{
    /// <summary>Keep both: the new file becomes "name (2).ext".</summary>
    KeepBoth,

    /// <summary>Leave the new file where it is.</summary>
    Skip
}

public sealed class Rule
{
    public RuleKind Kind { get; set; }
    public string Value { get; set; } = "";

    public Rule() { }
    public Rule(RuleKind kind, string value) { Kind = kind; Value = value; }

    public Rule Clone() => new(Kind, Value);
}

/// <summary>A folder files are moved into, with the rules that send files there.</summary>
public sealed class Destination
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public List<Rule> Rules { get; set; } = new();

    public Destination Clone() => new() { Id = Id, Name = Name, Path = Path, Rules = Rules.Select(r => r.Clone()).ToList() };
}

/// <summary>A folder that is watched (Downloads, Desktop…) and the destinations its files are sorted into.</summary>
public sealed class WatchFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<Destination> Destinations { get; set; } = new();

    public WatchFolder Clone() => new() { Id = Id, Name = Name, Path = Path, Enabled = Enabled, Destinations = Destinations.Select(d => d.Clone()).ToList() };
}

public sealed class AppSettings
{
    public const int CurrentVersion = 2;
    public const int MinQuietSeconds = 1, MaxQuietSeconds = 60;
    public const int MinRescanMinutes = 1, MaxRescanMinutes = 120;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>"auto", "en" or "tr".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>"system", "light" or "dark".</summary>
    public string Theme { get; set; } = "system";

    public bool Notifications { get; set; } = true;
    public bool Paused { get; set; }

    /// <summary>A file must stay unchanged this long before it is moved (downloads and copies in progress keep changing).</summary>
    public int QuietSeconds { get; set; } = 3;

    /// <summary>A full scan of every watched folder runs this often, in case a change notification was missed.</summary>
    public int RescanMinutes { get; set; } = 5;

    public ConflictPolicy Conflict { get; set; } = ConflictPolicy.KeepBoth;
    public List<WatchFolder> WatchFolders { get; set; } = new();

    public void Normalize()
    {
        Version = CurrentVersion;
        QuietSeconds = Math.Clamp(QuietSeconds, MinQuietSeconds, MaxQuietSeconds);
        RescanMinutes = Math.Clamp(RescanMinutes, MinRescanMinutes, MaxRescanMinutes);
        if (Language is not ("auto" or "en" or "tr")) Language = "auto";
        if (Theme is not ("system" or "light" or "dark")) Theme = "system";
        WatchFolders ??= new();
        foreach (var folder in WatchFolders)
        {
            folder.Destinations ??= new();
            foreach (var destination in folder.Destinations) destination.Rules ??= new();
        }
    }

    public AppSettings Clone() => new()
    {
        Version = Version, Language = Language, Theme = Theme, Notifications = Notifications, Paused = Paused,
        QuietSeconds = QuietSeconds, RescanMinutes = RescanMinutes, Conflict = Conflict,
        WatchFolders = WatchFolders.Select(w => w.Clone()).ToList()
    };
}

/// <summary>One move the engine made, kept so it can be undone.</summary>
public sealed record MoveRecord(string Id, DateTime TimeUtc, string From, string To, string WatchFolderId, string DestinationName)
{
    public DateTime? UndoneUtc { get; init; }
    public string FileName => System.IO.Path.GetFileName(To);
}

/// <summary>A move the engine would make right now (the preview).</summary>
public sealed record PlannedMove(string File, Destination Destination);
