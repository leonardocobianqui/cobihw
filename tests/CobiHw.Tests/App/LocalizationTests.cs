using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CobiHw.App.Localization;
using CobiHw.App.Resources;

namespace CobiHw.Tests.App;

/// <summary>Translations stay complete, XAML keys exist and language changes are announced.</summary>
[Collection(AppState.Name)]
public sealed partial class LocalizationTests : LocalizedTest
{
    private static Dictionary<string, string> Resx(string file) =>
        XDocument.Load(RepoFiles.App(Path.Combine("Resources", file))).Root!
            .Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")!.Value);

    // Placeholder numbers used by a text, e.g. "0,1" for "{0} of {1:0.0}".
    private static string Placeholders(string text) =>
        string.Join(",", PlaceholderPattern().Matches(text).Select(match => match.Groups[1].Value).Distinct().Order());

    [GeneratedRegex(@"\{(\d+)")]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"\{loc:Text (\w+)\}")]
    private static partial Regex XamlKeyPattern();

    [Theory]
    [InlineData("pt")]
    [InlineData("es")]
    [InlineData("ru")]
    public void Translation_HasTheSameKeysAndPlaceholdersAsEnglish(string language)
    {
        Dictionary<string, string> english = Resx("Strings.resx");
        Dictionary<string, string> translated = Resx($"Strings.{language}.resx");

        Assert.Empty(english.Keys.Except(translated.Keys));
        Assert.Empty(translated.Keys.Except(english.Keys));
        Assert.DoesNotContain(english.Keys, key => Placeholders(english[key]) != Placeholders(translated[key]));
    }

    [Fact]
    public void EveryKeyUsedInXaml_Exists()
    {
        Dictionary<string, string> english = Resx("Strings.resx");
        string[] keys =
        [
            .. Directory.EnumerateFiles(RepoFiles.App(""), "*.xaml", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .SelectMany(path => XamlKeyPattern().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
                .Distinct(),
        ];

        Assert.NotEmpty(keys);
        Assert.All(keys, key => Assert.True(english.ContainsKey(key), $"Missing key: {key}"));
    }

    [Theory]
    [InlineData("ru-RU", "ru-RU")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("pt-BR", "pt-BR")]
    public void Resolve_SupportedLanguages(string language, string expected) => Assert.Equal(expected, Localizer.Resolve(language).Name);

    [Fact]
    public void Resolve_UnknownLanguage_IsAutomatic() => Assert.Equal(Localizer.Resolve("auto"), Localizer.Resolve("xx"));

    [Fact]
    public void CultureChanged_RaisedOncePerRealChange()
    {
        int changes = 0;
        void Count(object? sender, EventArgs e) => changes++;
        Localizer.Instance.CultureChanged += Count;

        try
        {
            SetLanguage("en-US");
            Assert.Equal(1, changes);
            SetLanguage("es-ES");
            SetLanguage("ru-RU");
            SetLanguage("pt-BR");
            Assert.Equal(4, changes);
            SetLanguage("pt-BR");
            Assert.Equal(4, changes);
        }
        finally
        {
            Localizer.Instance.CultureChanged -= Count;
        }
    }

    [Fact]
    public void FixedTexts_AreTranslated()
    {
        Assert.StartsWith("Algo deu errado e o cobihw precisa fechar.", Strings.Error_Message, StringComparison.Ordinal);
        Assert.Equal("Travar posição e tamanho", Strings.Tray_LockPosition);

        SetLanguage("en-US");
        Assert.Equal("cobihw: opens the widget with administrator permission to measure FPS.", Strings.Task_Description);
        Assert.Equal("Copy details", Strings.Error_CopyDetails);

        SetLanguage("ru-RU");
        Assert.StartsWith("cobihw: открывает", Strings.Task_Description, StringComparison.Ordinal);
        Assert.Equal("Скопировано", Strings.Error_Copied);
    }
}
