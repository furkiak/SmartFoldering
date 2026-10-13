using CommunityToolkit.Mvvm.ComponentModel;
using SmartFoldering.Core;
using SmartFoldering.Localization;

namespace SmartFoldering.ViewModels;

/// <summary>A watched folder in the sidebar.</summary>
public sealed partial class WatchFolderItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _path = "";
    [ObservableProperty] private string _displayPath = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _exists;

    public WatchFolderItem(WatchFolder model)
    {
        Id = model.Id;
        Model = model;
        Update(model);
    }

    public string Id { get; }
    public WatchFolder Model { get; private set; }

    public void Update(WatchFolder model)
    {
        Model = model;
        Name = model.Name;
        Path = model.Path;
        DisplayPath = FriendlyPath.Of(model.Path);
        Enabled = model.Enabled;
        Exists = Directory.Exists(model.Path);
        Subtitle = !Exists ? Loc.Instance.Get("folder.missing")
                 : !Enabled ? Loc.Instance.Get("folder.off")
                 : Loc.Instance.Count("folder.destinations", model.Destinations.Count);
    }
}

/// <summary>A destination card in the middle column.</summary>
public sealed class DestinationItem
{
    private const int MaxChips = 9;

    public DestinationItem(Destination model, int index, int count)
    {
        Model = model;
        Look = Look.For(model);
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;

        var chips = model.Rules.SelectMany(rule => rule.Kind == RuleKind.Extension
            ? RuleMatcher.Extensions(rule.Value)
            : [RuleMatcher.Describe(rule)]).ToList();
        Chips = chips.Count <= MaxChips ? chips : [.. chips.Take(MaxChips - 1), $"+{chips.Count - (MaxChips - 1)}"];
    }

    public Destination Model { get; }
    public string Id => Model.Id;
    public string Name => Model.Name;
    public string Path => Model.Path;
    public string DisplayPath => FriendlyPath.Of(Model.Path);
    public Look Look { get; }
    public IReadOnlyList<string> Chips { get; }
    public bool HasRules => Chips.Count > 0;
    public bool CanMoveUp { get; }
    public bool CanMoveDown { get; }
}

/// <summary>One move in the activity list.</summary>
public sealed partial class ActivityItem : ObservableObject
{
    [ObservableProperty] private string _detail = "";

    public ActivityItem(MoveRecord record, DateTime nowUtc)
    {
        Record = record;
        Look = Look.ForFile(record.FileName);
        Refresh(nowUtc);
    }

    public MoveRecord Record { get; }
    public string FileName => Record.FileName;
    public Look Look { get; }
    public bool IsUndone => Record.UndoneUtc is not null;
    public bool CanUndo => !IsUndone;

    public void Refresh(DateTime nowUtc)
    {
        var loc = Loc.Instance;
        string when = loc.Ago(Record.UndoneUtc ?? Record.TimeUtc, nowUtc);
        Detail = IsUndone ? $"{loc.Get("activity.undone")} · {when}" : $"→ {Record.DestinationName} · {when}";
    }
}

/// <summary>A row of the preview.</summary>
public sealed record PreviewItem(string FileName, string Destination, Look Look);
