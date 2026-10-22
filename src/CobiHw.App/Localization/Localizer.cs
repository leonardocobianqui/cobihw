using System.ComponentModel;
using System.Globalization;
using CobiHw.App.Resources;

namespace CobiHw.App.Localization;

/// <summary>
/// Current UI language and the access point to translated texts.
/// </summary>
/// <remarks>
/// XAML binds to the indexer (through <see cref="TextExtension"/>), so raising "Item[]" updates every text on screen
/// without restarting the app.
/// </remarks>
public sealed class Localizer : INotifyPropertyChanged
{
    /// <summary>Saved value for "follow the Windows language".</summary>
    public const string AutomaticLanguage = "auto";

    // Captured at startup, before SetLanguage overwrites CurrentUICulture.
    private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;
    // Used when Windows is in a language the app doesn't have.
    private static readonly CultureInfo FallbackCulture = CultureInfo.GetCultureInfo("en-US");

    private static readonly IReadOnlyList<LanguageOption> FixedLanguages =
    [
        new("pt-BR", "Português (Brasil)"),
        new("en-US", "English"),
        new("es-ES", "Español"),
        new("ru-RU", "Русский"),
    ];

    private Localizer() => Culture = Resolve(AutomaticLanguage);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changes, for texts built in code (formatted values, tray menu).</summary>
    public event EventHandler? CultureChanged;

    /// <summary>The single instance the whole app shares.</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>Culture used for texts, numbers and dates.</summary>
    public CultureInfo Culture { get; private set; }

    /// <summary>Translated text for a resource key; the key itself if it's missing, so gaps are visible instead of blank.</summary>
    public string this[string key] => Strings.ResourceManager.GetString(key, Culture) ?? key;

    /// <summary>Options for the language box: "Automatic" (translated) plus each language in its own name.</summary>
    public static IReadOnlyList<LanguageOption> Languages() =>
        [new(AutomaticLanguage, Strings.Language_Automatic), .. FixedLanguages];

    /// <summary><see cref="string.Format(IFormatProvider, string, object[])"/> with the current culture.</summary>
    public static string Format(string format, params object?[] args) =>
        string.Format(Instance.Culture, format, args);

    /// <summary>Culture for a saved language value.</summary>
    /// <param name="language">A language code, or <see cref="AutomaticLanguage"/>.</param>
    /// <returns>The fixed language; for "auto", Windows' language when supported (any region), otherwise English.</returns>
    public static CultureInfo Resolve(string? language)
    {
        LanguageOption? fixedLanguage = FixedLanguages.FirstOrDefault(option => option.Code == language);
        if (fixedLanguage is not null)
        {
            return CultureInfo.GetCultureInfo(fixedLanguage.Code);
        }

        bool supported = FixedLanguages.Any(option =>
            CultureInfo.GetCultureInfo(option.Code).TwoLetterISOLanguageName == WindowsCulture.TwoLetterISOLanguageName);

        return supported ? WindowsCulture : FallbackCulture;
    }

    /// <summary>Switches the language for the whole app and notifies every binding.</summary>
    public void SetLanguage(string? language)
    {
        CultureInfo culture = Resolve(language);
        // Code reads Strings.X directly, so the resource class must follow too.
        Strings.Culture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (culture.Equals(Culture))
        {
            return;
        }

        Culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One entry of the language box.</summary>
/// <param name="Code">Saved value ("pt-BR", "auto"…).</param>
/// <param name="Name">Text shown in the box.</param>
public sealed record LanguageOption(string Code, string Name);
