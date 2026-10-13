using SmartFoldering.Core;
using SmartFoldering.Localization;

namespace SmartFoldering.ViewModels;

/// <summary>What would move in one folder right now. Built from <see cref="Organizer.Preview"/>; moves nothing.</summary>
public sealed class PreviewViewModel
{
    private const int MaxRows = 200;

    public PreviewViewModel(WatchFolder folder, IReadOnlyList<PlannedMove> plan)
    {
        Folder = folder;
        Items = plan.Take(MaxRows).Select(p => new PreviewItem(Path.GetFileName(p.File), p.Destination.Name, Look.For(p.Destination))).ToList();
        Count = plan.Count;
        var loc = Loc.Instance;
        CountText = loc.Count("preview.count", plan.Count);
        MoreText = plan.Count > MaxRows ? loc.Format("preview.more", plan.Count - MaxRows) : null;
        Subtitle = loc.Format("preview.subtitle", folder.Name);
        ActionText = loc.Get(folder.Enabled ? "preview.sortnow" : "preview.start");
    }

    public WatchFolder Folder { get; }
    public IReadOnlyList<PreviewItem> Items { get; }
    public int Count { get; }
    public bool IsEmpty => Count == 0;
    public string CountText { get; }
    public string? MoreText { get; }
    public bool HasMore => MoreText is not null;
    public string Subtitle { get; }
    public string ActionText { get; }

    /// <summary>Turning sorting on is always possible; "sort now" only makes sense when there is something to sort.</summary>
    public bool CanAct => !Folder.Enabled || Count > 0;
}
