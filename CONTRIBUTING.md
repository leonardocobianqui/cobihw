# Contributing

Thanks for your interest in cobihw. This guide covers how to build, test and change the code. For how the code is organized, see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Reporting bugs and ideas

Open an [issue](https://github.com/leonardocobianqui/cobihw/issues). For bugs, include:

- Windows version and the cobihw version (**About cobihw** in the tray menu).
- What you did, what you expected and what happened.
- If the app showed the error window, the text from **Copy details**.

## Setup

- Windows 10 or 11, 64-bit.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Visual Studio is optional; the `dotnet` CLI is enough.

```powershell
dotnet build cobihw.slnx
dotnet run --project src/CobiHw.App
```

`src/CobiHw.ConsoleTest` prints one set of readings, which is handy to compare with Task Manager:

```powershell
dotnet run --project src/CobiHw.ConsoleTest
```

## Tests

```powershell
dotnet test --project tests/CobiHw.Tests/CobiHw.Tests.csproj
```

Or, in Visual Studio: **Test → Test Explorer → Run All**.

- Tests use [xUnit v3](https://xunit.net/) on Microsoft Testing Platform; `global.json` sets `dotnet test` to use it.
- Every test class joins the same collection (`[Collection(AppState.Name)]`), so tests run one at a time: the app language is global.
- Classes that check texts derive from `LocalizedTest`, which starts each test in Brazilian Portuguese. Use `SetLanguage` to test another language.
- Tests that touch files use `TempSettingsFile`, which works in its own temporary folder.
- The speed test runs against `FakeCloudflare`, a simulated server; no test uses the internet.
- The [build workflow](.github/workflows/build.yml) builds and runs the tests on every push to `main` and on every pull request.

Add or update tests for every change in behavior.

## Code conventions

- **Warnings are errors** in every project (`TreatWarningsAsErrors`). Fix them; don't suppress them without a reason in a comment.
- **Style:** `.editorconfig` describes it (file-scoped namespaces, `_camelCase` private fields, `PascalCase` constants, explicit types except when the type is obvious). Its rules show up as suggestions in the editor.
- **Comments, in English:**
  - XML documentation (`///`) on every type and member: one short `<summary>`, plus `<param>` and `<returns>` where they help.
  - `//` only where the code doesn't explain itself: the reason for a choice, a Windows quirk, a unit. Keep it to one line when possible.
  - XAML: one `<!-- -->` per section, tab or style, not per element.
  - Windows API declarations say which official structure or constant they mirror.
- **Line endings** are normalized by `.gitattributes`; you don't need to configure Git.

## Texts and translations

All user-visible texts live in `src/CobiHw.App/Resources`:

| File | Language |
| --- | --- |
| `Strings.resx` | English (default; also used for any language without a translation) |
| `Strings.pt.resx` | Português (Brasil) |
| `Strings.es.resx` | Español |
| `Strings.ru.resx` | Русский |

To add or change a text:

1. Add the entry to `Strings.resx` with a `<comment>` for translators: where it appears and what each `{0}` means. Visual Studio updates the generated `Strings.Designer.cs` when you save the file (or right-click `Strings.resx` → **Run Custom Tool**); `dotnet build` doesn't regenerate it.
2. Add the same key to every other `Strings.*.resx`, keeping the same `{0}`, `{1}`… placeholders.
3. In XAML, use `{loc:Text Key}`; in code, `Strings.Key` (and `Localizer.Format` for texts with placeholders).

The tests fail if a language misses a key, has different placeholders, or if XAML uses a key that doesn't exist.

To add a language: create `Strings.<culture>.resx` with every key, add the culture to `FixedLanguages` in `Localization/Localizer.cs`, and add it to the `[InlineData]` list of `LocalizationTests`.

## Releases

1. Update `Version` and `ReleaseDate` in `Directory.Build.props`.
2. Add the version to `CHANGELOG.md`.
3. Run `.\publish.ps1`. It creates, in `publish/<version>`:
   - `cobihw-<version>-portable-win-x64.zip`: includes .NET.
   - `cobihw-<version>-framework-dependent-win-x64.zip`: needs the .NET 10 Desktop Runtime.
   - `SHA256SUMS.txt`.
4. Create a GitHub release with the tag `v<version>` and attach those three files.

The update check in **About** reads the latest GitHub release and compares its tag with the app version.

## Pull requests

- Keep each pull request to one subject.
- Use [Conventional Commits](https://www.conventionalcommits.org/) for commit messages, e.g. `fix: keep the widget inside the screen after a monitor is removed`.
- Make sure the build has no warnings and the tests pass.
