using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CobiHw.App.Settings;

/// <summary>
/// Reads and writes cobihw.settings.json next to the executable, with one profile per computer and Windows user.
/// </summary>
/// <remarks>
/// Writes go to a temporary file that replaces the original, under a lock file, so a crash or a second
/// instance (e.g. the copy running as administrator for FPS) can't leave a half-written file.
/// Profiles of other computers are kept as raw JSON, so newer fields written by another version survive.
/// </remarks>
internal sealed class SettingsStore
{
    private const string FileName = "cobihw.settings.json";
    // 2 = { Format, Profiles[] }; version 1 was a single AppSettings object.
    private const int FormatVersion = 2;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ProfileIdentity _identity;
    private readonly bool _folderWritable;
    private List<JsonObject> _profiles = [];
    // Last saved content, to skip writes when nothing changed.
    private byte[]? _lastWritten;

    private SettingsStore(string filePath, ProfileIdentity identity, bool folderWritable)
    {
        FilePath = filePath;
        _identity = identity;
        _folderWritable = folderWritable;
    }

    /// <summary>Full path of the settings file.</summary>
    public string FilePath { get; }

    /// <summary>Whether saving is possible, decided by the last load.</summary>
    public SettingsAccess Access { get; private set; } = SettingsAccess.Writable;

    /// <summary>True when changes can't be saved.</summary>
    public bool IsReadOnly => Access != SettingsAccess.Writable;

    /// <summary>Store for an explicit path (used by tests).</summary>
    public static SettingsStore At(string filePath, ProfileIdentity identity) =>
        new(Path.GetFullPath(filePath), identity, folderWritable: true);

    /// <summary>Store next to the executable, so the app stays portable.</summary>
    public static SettingsStore CreateBesideExecutable(ProfileIdentity identity)
    {
        string executableDirectory = AppContext.BaseDirectory;
        return new SettingsStore(Path.Combine(executableDirectory, FileName), identity, IsWritable(executableDirectory));
    }

    /// <summary>Loads this profile without waiting for a busy file.</summary>
    public AppSettings Load() => Load(TimeSpan.Zero);

    /// <summary>Loads this profile, retrying while another process holds the file.</summary>
    /// <param name="patience">How long to keep retrying.</param>
    /// <returns>The settings; defaults (and read-only access) if the file stays busy.</returns>
    public AppSettings Load(TimeSpan patience)
    {
        long deadline = Environment.TickCount64 + (long)patience.TotalMilliseconds;

        while (true)
        {
            if (TryReadProfiles() is List<JsonObject> profiles)
            {
                _profiles = profiles;
                Access = _folderWritable ? SettingsAccess.Writable : SettingsAccess.FolderNotWritable;
                AppSettings settings = OwnSettings(profiles);
                _lastWritten = Serialize(settings);
                return settings;
            }

            if (Environment.TickCount64 >= deadline)
            {
                Access = _folderWritable ? SettingsAccess.FileInUse : SettingsAccess.FolderNotWritable;
                return new AppSettings();
            }

            Thread.Sleep(RetryDelay);
        }
    }

    /// <summary>True when the file can be read right now.</summary>
    public bool CanRead() => TryReadProfiles() is not null;

    /// <summary>Saves this profile, keeping the other profiles in the file.</summary>
    /// <returns>False when read-only or the file is busy.</returns>
    public bool Save(AppSettings settings)
    {
        if (IsReadOnly)
        {
            return false;
        }

        byte[] content = Serialize(settings);

        if (_lastWritten is not null && content.AsSpan().SequenceEqual(_lastWritten))
        {
            return true;
        }

        if (!Rewrite(profiles => Replace(profiles, OwnProfile(settings))))
        {
            return false;
        }

        _lastWritten = content;
        return true;
    }

    /// <summary>Saves, retrying while the file is busy (used on exit).</summary>
    public bool Save(AppSettings settings, TimeSpan patience)
    {
        long deadline = Environment.TickCount64 + (long)patience.TotalMilliseconds;

        while (!Save(settings))
        {
            if (IsReadOnly || Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Thread.Sleep(RetryDelay);
        }

        return true;
    }

    /// <summary>Profiles of other computers and users in the file.</summary>
    public IReadOnlyList<StoredProfile> ListOtherProfiles()
    {
        List<JsonObject> profiles = TryReadProfiles() ?? _profiles;

        return [.. profiles
            .Where(profile => !IsOwn(profile))
            .Select(profile => new StoredProfile(
                Text(profile, "Computer") ?? string.Empty,
                Text(profile, "UserName") ?? string.Empty,
                LastUsed(profile)))];
    }

    /// <summary>Keeps only this profile in the file.</summary>
    public bool RemoveOtherProfiles(AppSettings settings)
    {
        if (IsReadOnly || !Rewrite(_ => [OwnProfile(settings)]))
        {
            return false;
        }

        _lastWritten = Serialize(settings);
        return true;
    }

    // Read-modify-write under the lock file, re-reading first so changes from another instance aren't lost.
    private bool Rewrite(Func<List<JsonObject>, List<JsonObject>> change)
    {
        string temporaryPath = FilePath + ".tmp";

        try
        {
            // FileShare.None makes this an exclusive lock between processes; DeleteOnClose removes it afterwards.
            using FileStream gate = new(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);

            if (TryReadProfiles() is not List<JsonObject> current)
            {
                return false;
            }

            List<JsonObject> profiles = change(current);
            var document = new JsonObject
            {
                ["Format"] = FormatVersion,
                ["Profiles"] = new JsonArray([.. profiles]),
            };

            using (FileStream stream = File.Create(temporaryPath))
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions));
                stream.Flush(flushToDisk: true);
            }

            // Replacing in one step means readers see either the old file or the new one, never a partial one.
            File.Move(temporaryPath, FilePath, overwrite: true);
            _profiles = [.. profiles.Select(profile => profile.DeepClone().AsObject())];
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(temporaryPath);
            return false;
        }
    }

    // Null = file busy; an empty list = no file yet (or an invalid one that was set aside).
    private List<JsonObject>? TryReadProfiles()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllBytes(FilePath));

            if (root is JsonObject document && document["Profiles"] is JsonArray profiles)
            {
                return [.. profiles.OfType<JsonObject>().Select(profile => profile.DeepClone().AsObject())];
            }

            // Format 1 file: its settings become this computer's profile.
            AppSettings legacy = root?.Deserialize<AppSettings>(JsonOptions) ?? new AppSettings();
            return [OwnProfile(legacy)];
        }
        catch (JsonException)
        {
            SetAsideInvalidFile();
            return [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // No own profile yet: start from the most recently used one.
    private AppSettings OwnSettings(List<JsonObject> profiles)
    {
        if (profiles.FirstOrDefault(IsOwn) is JsonObject own)
        {
            return SettingsOf(own) ?? new AppSettings();
        }

        JsonObject? latest = profiles.OrderByDescending(LastUsed).FirstOrDefault();
        return latest is not null && SettingsOf(latest) is AppSettings previous ? previous.ForNewComputer() : new AppSettings();
    }

    private JsonObject OwnProfile(AppSettings settings) => new()
    {
        ["Computer"] = _identity.Computer,
        ["UserId"] = _identity.UserId,
        ["UserName"] = _identity.UserName,
        ["LastUsed"] = DateTimeOffset.UtcNow,
        ["Settings"] = JsonSerializer.SerializeToNode(settings, JsonOptions),
    };

    private List<JsonObject> Replace(List<JsonObject> profiles, JsonObject own)
    {
        List<JsonObject> result = [.. profiles.Where(profile => !IsOwn(profile))];
        int index = profiles.FindIndex(IsOwn);
        result.Insert(index < 0 ? result.Count : Math.Min(index, result.Count), own);
        return result;
    }

    private bool IsOwn(JsonObject profile) => _identity.Matches(Text(profile, "Computer"), Text(profile, "UserId"));

    private static AppSettings? SettingsOf(JsonObject profile)
    {
        try
        {
            return profile["Settings"]?.Deserialize<AppSettings>(JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonObject profile, string name) =>
        profile[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static DateTimeOffset? LastUsed(JsonObject profile) =>
        profile["LastUsed"] is JsonValue value && value.TryGetValue(out DateTimeOffset moment) ? moment : null;

    // Keeps a corrupt file as ".invalid" instead of overwriting it, so the user can recover it.
    private void SetAsideInvalidFile()
    {
        try
        {
            File.Move(FilePath, FilePath + ".invalid", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static byte[] Serialize(AppSettings settings) => JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    // Probes with a real file, since permissions alone don't cover Controlled Folder Access or read-only media.
    private static bool IsWritable(string directory)
    {
        string probePath = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(probePath, string.Empty);
            File.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
