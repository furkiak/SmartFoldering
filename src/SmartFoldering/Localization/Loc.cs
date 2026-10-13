using System.ComponentModel;
using System.Globalization;

namespace SmartFoldering.Localization;

/// <summary>
/// Runtime-switchable string table (English / Turkish). XAML binds through the indexer (see <see cref="TExtension"/>),
/// so changing the language updates every open window at once.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private IReadOnlyDictionary<string, string> _table = Strings.English;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changed so view models can rebuild text they computed themselves.</summary>
    public event Action? LanguageChanged;

    public string Language { get; private set; } = "en";
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public string this[string key] => Get(key);

    public string Get(string key) =>
        _table.TryGetValue(key, out var value) ? value
        : Strings.English.TryGetValue(key, out var fallback) ? fallback
        : key;

    public string Format(string key, params object[] args) => string.Format(Culture, Get(key), args);

    /// <summary>"1 file" / "3 files": uses <c>key.one</c> for one and <c>key.many</c> (with {0}) otherwise.</summary>
    public string Count(string key, int count) => count == 1 ? Get(key + ".one") : Format(key + ".many", count);

    /// <summary>Applies "auto", "en" or "tr". "auto" follows the operating system's UI language.</summary>
    public void SetLanguage(string setting)
    {
        string resolved = setting switch
        {
            "tr" => "tr",
            "en" => "en",
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en"
        };
        if (resolved == Language) return;

        Language = resolved;
        Culture = CultureInfo.GetCultureInfo(resolved == "tr" ? "tr-TR" : "en-US");
        _table = resolved == "tr" ? Strings.Turkish : Strings.English;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke();
    }

    /// <summary>"just now", "5 min ago", "3 h ago", "yesterday 14:05", "12 Mar 09:30".</summary>
    public string Ago(DateTime utc, DateTime nowUtc)
    {
        TimeSpan age = nowUtc - utc;
        if (age < TimeSpan.FromMinutes(1)) return Get("time.now");
        if (age < TimeSpan.FromHours(1)) return Format("time.minutes", (int)age.TotalMinutes);
        if (age < TimeSpan.FromHours(12)) return Format("time.hours", (int)age.TotalHours);
        DateTime local = utc.ToLocalTime(), today = nowUtc.ToLocalTime().Date;
        if (local.Date == today) return local.ToString("t", Culture);
        if (local.Date == today.AddDays(-1)) return Get("time.yesterday") + " " + local.ToString("t", Culture);
        return local.ToString("d MMM HH:mm", Culture);
    }
}
