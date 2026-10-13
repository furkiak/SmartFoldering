using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SmartFoldering.Core;

namespace SmartFoldering.ViewModels;

/// <summary>An icon and a colour for a destination or a file, so the same kind of file looks the same everywhere.</summary>
public sealed record Look(Geometry? Icon, IBrush Accent, IBrush Soft)
{
    private static readonly Dictionary<string, (string Icon, string Color)> ByTemplate = new()
    {
        ["documents"] = ("IconDocument", "#0A84FF"),
        ["images"] = ("IconImage", "#FF9F0A"),
        ["videos"] = ("IconVideo", "#FF375F"),
        ["music"] = ("IconMusic", "#BF5AF2"),
        ["archives"] = ("IconArchive", "#C69C6D"),
        ["installers"] = ("IconDownload", "#30D158"),
        ["code"] = ("IconCode", "#40C8E0"),
    };

    public static Look For(Destination destination)
    {
        var first = destination.Rules.FirstOrDefault();
        if (first is null) return Make("IconFolder", "#8E8E93");
        if (RuleTemplates.For(first) is { } template) return ForTemplate(template.Key);
        return first.Kind switch
        {
            RuleKind.NameContains => Make("IconTag", "#FFD60A"),
            RuleKind.NameRegex => Make("IconAsterisk", "#5E5CE6"),
            _ => Make("IconFolder", "#8E8E93")
        };
    }

    public static Look ForFile(string fileName)
    {
        var template = RuleTemplates.All.FirstOrDefault(t => RuleMatcher.Matches(new Rule(RuleKind.Extension, t.Extensions), fileName));
        return template is null ? Make("IconDocument", "#8E8E93") : ForTemplate(template.Key);
    }

    /// <summary>A green tick: several files sorted, or something went well.</summary>
    public static Look Success => Make("IconCheck", "#30D158");

    /// <summary>The app's own blue: hints and welcome messages.</summary>
    public static Look Info => Make("IconFolder", "#0A84FF");

    private static Look ForTemplate(string key) =>
        ByTemplate.TryGetValue(key, out var look) ? Make(look.Icon, look.Color) : Make("IconFolder", "#8E8E93");

    private static Look Make(string icon, string color)
    {
        var c = Color.Parse(color);
        return new Look(Geometry(icon), new SolidColorBrush(c), new SolidColorBrush(Color.FromArgb(0x38, c.R, c.G, c.B)));
    }

    public static Geometry? Geometry(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) ? value as Geometry : null;
}
