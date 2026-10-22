using CobiHw.App.Localization;

namespace CobiHw.Tests;

/// <summary>
/// Base class for the test classes. Each test starts in Brazilian Portuguese, the app's main language,
/// so expected texts match what users see.
/// </summary>
/// <remarks>
/// The language is global state: test classes join the <see cref="AppState"/> collection so they never run in parallel.
/// </remarks>
public abstract class LocalizedTest
{
    /// <summary>Language every test starts in.</summary>
    protected const string TestLanguage = "pt-BR";

    /// <summary>Resets the app language before each test.</summary>
    protected LocalizedTest() => SetLanguage(TestLanguage);

    /// <summary>Switches the app language, as the settings window does.</summary>
    /// <param name="language">Culture name, e.g. "en-US".</param>
    protected static void SetLanguage(string language) => Localizer.Instance.SetLanguage(language);
}
