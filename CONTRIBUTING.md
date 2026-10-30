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
- The [build workflow](.github/workflows/build.yml) builds and runs the tests on every push to `main` and on every pull request, in a 64-bit and in a 32-bit process.

Add or update tests for every change in behavior.

## Code conventions

- **Warnings are errors** in every project (`TreatWarningsAsErrors`). Fix them; don't suppress them without a reason in a comment.
- **Style:** `.editorconfig` describes it (file-scoped namespaces, `_camelCase` private fields, `PascalCase` constants, explicit types except when the type is obvious). Its rules show up as suggestions in the editor.
- **Comments, in English:**
  - XML documentation (`///`) on every type and member: one short `<summary>`, plus `<param>` and `<returns>` where they help.
  - `//` only where the code doesn't explain itself: the reason for a choice, a Windows quirk, a unit. Keep it to one line when possible.
  - XAML: one `<!-- -->` per section, tab or style, not per element.
  - Windows API declarations say which official structure or constant they mirror.
- **64-bit and 32-bit:** every Windows API declaration must work in both.
  - Use `nint`/`nuint` where the SDK has a pointer, a handle or `SIZE_T`.
  - Add each new structure to `InteropLayoutTests` (or `User32Tests` in the App) with its x64 and x86 sizes from the Windows SDK headers.
  - Some `...Ptr` functions only exist in 64-bit Windows; see `User32.GetWindowLongPtr`.
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
3. Run `pwsh ./publish.ps1` (PowerShell 7). It creates, in `publish/<version>`:
   - `cobihw-<version>-portable-win-x64.zip` and `...-win-x86.zip`: include .NET.
   - `cobihw-<version>-framework-dependent-win-x64.zip` and `...-win-x86.zip`: need the .NET 10 Desktop Runtime of the same architecture.
   - `SHA256SUMS.txt`, with the checksums of every zip.
   - `-Architecture x64` (or `x86`) publishes only one architecture.
4. Run the x64 packages on 64-bit Windows and the x86 packages on 32-bit Windows 10.
5. Create a GitHub release titled `cobihw <version>` with the tag `v<version>`, the notes below, and the four zips plus `SHA256SUMS.txt` attached.

### Release notes

The website lists the latest releases with their notes, so every release uses the same structure. The sections come straight from the version's `CHANGELOG.md` entry, in the same order and with the same names; leave out the ones that don't apply.

```markdown
<One sentence on what the release is about.>

### Added
- ...

### Changed
- ...

### Fixed
- ...

### Downloads
| File | Windows | Requires |
| --- | --- | --- |
| `cobihw-<version>-portable-win-x64.zip` | 64-bit | Nothing; .NET included |
| `cobihw-<version>-framework-dependent-win-x64.zip` | 64-bit | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) |
| `cobihw-<version>-portable-win-x86.zip` | 32-bit | Nothing; .NET included |
| `cobihw-<version>-framework-dependent-win-x86.zip` | 32-bit | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x86) |

Not sure which one? **Settings → System → About → System type**. `SHA256SUMS.txt` has the checksum of every zip.

**Full changelog:** https://github.com/leonardocobianqui/cobihw/compare/v<previous>...v<version>
```

Upgrade steps or known issues, when there are any, go in a `### Notes` section right before `### Downloads`.

The update check in **About** reads the latest GitHub release, compares its tag with the app version and only offers it when it has a zip for the architecture of the running app (`-win-x64.zip` or `-win-x86.zip`). Keep those package names.

## Pull requests

- Keep each pull request to one subject.
- Use [Conventional Commits](https://www.conventionalcommits.org/) for commit messages, e.g. `fix: keep the widget inside the screen after a monitor is removed`.
- Make sure the build has no warnings and the tests pass.
