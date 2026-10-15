using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CobiHw.App.Settings;

internal sealed class SettingsStore
{
    private const string FileName = "cobihw.settings.json";
    private const string AppDataFolderName = "cobihw";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private SettingsStore(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public static SettingsStore At(string filePath) => new(Path.GetFullPath(filePath));

    public static SettingsStore CreateBesideExecutable()
    {
        string executableDirectory = AppContext.BaseDirectory;

        if (IsWritable(executableDirectory))
        {
            return new SettingsStore(Path.Combine(executableDirectory, FileName));
        }

        string appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName);
        Directory.CreateDirectory(appDataDirectory);

        return new SettingsStore(Path.Combine(appDataDirectory, FileName));
    }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        try
        {
            using FileStream stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            File.Move(FilePath, FilePath + ".invalido", overwrite: true);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string temporaryPath = FilePath + ".tmp";

        using (FileStream stream = File.Create(temporaryPath))
        {
            JsonSerializer.Serialize(stream, settings, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    private static bool IsWritable(string directory)
    {
        string probePath = Path.Combine(directory, $".teste-escrita-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
