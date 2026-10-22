using CobiHw.App.Settings;

namespace CobiHw.Tests;

/// <summary>Settings file in its own temporary folder, deleted when the test ends.</summary>
internal sealed class TempSettingsFile : IDisposable
{
    /// <summary>PC and user the default <see cref="Store"/> belongs to.</summary>
    public static readonly ProfileIdentity Profile = new("NOTEBOOK", "S-1-5-21-100", "cobia");

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cobihw-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the folder; the file itself only exists after the first save or write.</summary>
    public TempSettingsFile()
    {
        Directory.CreateDirectory(_folder);
        FilePath = Path.Combine(_folder, "cobihw.settings.json");
        Store = For(Profile);
    }

    /// <summary>Full path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>Store for <see cref="Profile"/>.</summary>
    public SettingsStore Store { get; }

    /// <summary>A new store on the same file, as another app start or another PC would open it.</summary>
    /// <param name="profile">PC and user of that store.</param>
    public SettingsStore For(ProfileIdentity profile) => SettingsStore.At(FilePath, profile);

    /// <summary>Raw file content.</summary>
    public string Read() => File.ReadAllText(FilePath);

    /// <summary>Replaces the file content, e.g. with a file saved by an older version.</summary>
    /// <param name="json">New content.</param>
    public void Write(string json) => File.WriteAllText(FilePath, json);

    /// <summary>Deletes the folder; a file still locked by a failed test is left for the system to clean.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
