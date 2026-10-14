using System.ComponentModel;
using System.Globalization;
using CobiHw.App.Resources;

namespace CobiHw.App.Localization;

public sealed class Localizer : INotifyPropertyChanged
{
    public const string AutomaticLanguage = "auto";

    private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;
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

    public event EventHandler? CultureChanged;

    public static Localizer Instance { get; } = new();

    public CultureInfo Culture { get; private set; }

    public string this[string key] => Strings.ResourceManager.GetString(key, Culture) ?? key;

    public static IReadOnlyList<LanguageOption> Languages() =>
        [new(AutomaticLanguage, Strings.Language_Automatic), .. FixedLanguages];

    public static string Format(string format, params object?[] args) =>
        string.Format(Instance.Culture, format, args);

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

    public void SetLanguage(string? language)
    {
        CultureInfo culture = Resolve(language);
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

public sealed record LanguageOption(string Code, string Name);
