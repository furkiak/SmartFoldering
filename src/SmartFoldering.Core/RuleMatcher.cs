using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SmartFoldering.Core;

/// <summary>Decides where a file goes. Destinations are tried in order; the first one with a matching rule wins.</summary>
public static class RuleMatcher
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();
    private static readonly char[] ListSeparators = [',', ';', ' ', '\t'];

    public static Destination? FindDestination(WatchFolder folder, string fileName)
    {
        foreach (var destination in folder.Destinations)
            if (destination.Rules.Any(rule => Matches(rule, fileName)))
                return destination;
        return null;
    }

    public static bool Matches(Rule rule, string fileName)
    {
        if (string.IsNullOrWhiteSpace(rule.Value) || string.IsNullOrEmpty(fileName)) return false;
        return rule.Kind switch
        {
            RuleKind.Extension => Extensions(rule.Value).Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) && fileName.Length > ext.Length),
            RuleKind.NameContains => System.IO.Path.GetFileNameWithoutExtension(fileName).Contains(rule.Value.Trim(), StringComparison.OrdinalIgnoreCase),
            RuleKind.NameRegex => MatchesRegex(rule.Value, fileName),
            _ => false
        };
    }

    /// <summary>"pdf, .DOCX;tar.gz" -> ".pdf", ".docx", ".tar.gz".</summary>
    public static IReadOnlyList<string> Extensions(string value) =>
        value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
             .Select(e => "." + e.TrimStart('*', '.').ToLowerInvariant())
             .Where(e => e.Length > 1)
             .Distinct()
             .ToArray();

    /// <summary>Null when the rule is usable, otherwise a short reason (a localisation key).</summary>
    public static string? Validate(Rule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Value)) return "rule.error.empty";
        return rule.Kind switch
        {
            RuleKind.Extension when Extensions(rule.Value).Count == 0 => "rule.error.extension",
            RuleKind.NameRegex when CompileRegex(rule.Value) is null => "rule.error.regex",
            _ => null
        };
    }

    /// <summary>A short label for chips in the UI: ".pdf .docx", "“invoice”", "/^IMG_\d+/".</summary>
    public static string Describe(Rule rule) => rule.Kind switch
    {
        RuleKind.Extension => string.Join(" ", Extensions(rule.Value)),
        RuleKind.NameContains => $"“{rule.Value.Trim()}”",
        _ => $"/{rule.Value}/"
    };

    private static bool MatchesRegex(string pattern, string fileName)
    {
        var regex = CompileRegex(pattern);
        if (regex is null) return false;
        try
        {
            return regex.IsMatch(fileName);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static Regex? CompileRegex(string pattern) => RegexCache.GetOrAdd(pattern, p =>
    {
        try
        {
            return new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    });
}

/// <summary>Ready-made destinations for a typical Downloads folder.</summary>
public static class RuleTemplates
{
    public sealed record Template(string Key, string FolderName, string Extensions);

    public static IReadOnlyList<Template> All { get; } =
    [
        new("documents", "Documents", "pdf, doc, docx, odt, rtf, txt, md, xls, xlsx, ods, csv, ppt, pptx, odp, epub"),
        new("images", "Images", "jpg, jpeg, png, gif, webp, heic, heif, avif, bmp, tif, tiff, svg, ico, raw, psd"),
        new("videos", "Videos", "mp4, mov, mkv, avi, webm, wmv, m4v, flv"),
        new("music", "Music", "mp3, wav, flac, m4a, aac, ogg, opus, wma"),
        new("archives", "Archives", "zip, rar, 7z, tar, gz, tgz, bz2, xz, zst, iso"),
        new("installers", "Installers", "exe, msi, msix, appx, dmg, pkg, deb, rpm, appimage, apk"),
        new("code", "Code", "json, xml, yml, yaml, toml, py, js, ts, cs, java, go, rs, html, css, sql, sh, ps1, ipynb")
    ];

    /// <summary>
    /// One destination per template, as sub-folders of <paramref name="root"/>. <paramref name="displayName"/> maps a
    /// template key to a translated name, used for the destination and its folder ("Belgeler" instead of "Documents").
    /// </summary>
    public static List<Destination> Create(string root, Func<string, string>? displayName = null) =>
        All.Select(t =>
        {
            string name = displayName?.Invoke(t.Key) is { Length: > 0 } translated ? translated : t.FolderName;
            return new Destination
            {
                Name = name,
                Path = System.IO.Path.Combine(root, name),
                Rules = { new Rule(RuleKind.Extension, t.Extensions) }
            };
        }).ToList();

    /// <summary>The template whose extensions include the first extension of <paramref name="rule"/>, if any.</summary>
    public static Template? For(Rule rule)
    {
        if (rule.Kind != RuleKind.Extension) return null;
        string? first = RuleMatcher.Extensions(rule.Value).FirstOrDefault();
        return first is null ? null : All.FirstOrDefault(t => RuleMatcher.Extensions(t.Extensions).Contains(first));
    }
}
