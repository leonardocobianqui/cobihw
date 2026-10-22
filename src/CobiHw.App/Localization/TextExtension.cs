using System.Windows.Data;
using System.Windows.Markup;

namespace CobiHw.App.Localization;

/// <summary>
/// XAML markup extension for translated texts: <c>Text="{loc:Text Settings_Title}"</c>.
/// </summary>
/// <remarks>Creates a binding to <see cref="Localizer"/>, so the text updates when the language changes.</remarks>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TextExtension : MarkupExtension
{
    /// <summary>Creates the extension for a resource key.</summary>
    public TextExtension(string key) => Key = key;

    /// <summary>Resource key in Strings.resx.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
