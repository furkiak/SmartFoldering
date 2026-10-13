using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace SmartFoldering.Localization;

/// <summary>XAML shorthand for a localized string: <c>Text="{loc:T settings.title}"</c>.</summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]", BindingMode.OneWay) { Source = Loc.Instance };
}
