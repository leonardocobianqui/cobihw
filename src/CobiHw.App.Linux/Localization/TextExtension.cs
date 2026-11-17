using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CobiHw.Presentation.Localization;

namespace CobiHw.App.Linux.Localization;

/// <summary>
/// {loc:Text Key} in XAML: the translated text, which follows a language change without reopening the window.
/// </summary>
/// <remarks>Binds to the <see cref="Localizer"/> indexer, which raises "Item[]" when the language changes; the same keys as the Windows app.</remarks>
public sealed class TextExtension(string key) : MarkupExtension
{
    /// <summary>Resource key, e.g. Metric_Cpu.</summary>
    public string Key { get; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
}
