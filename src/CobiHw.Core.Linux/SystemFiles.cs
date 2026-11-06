using System.Globalization;

namespace CobiHw.Core.Linux;

/// <summary>
/// Read access to /proc and /sys under a root folder: "/" on a real system, a test folder in the tests.
/// </summary>
/// <remarks>
/// Every read tolerates a missing or unreadable file and returns nothing, because what exists varies by kernel, driver and
/// hardware: no battery, no cpufreq in a VM, a disk unplugged between listing it and reading it.
/// </remarks>
internal sealed class SystemFiles(string root)
{
    /// <summary>The running system.</summary>
    public static SystemFiles Live { get; } = new("/");

    /// <summary>Text of a file, without the trailing newline or the NUL padding some sysfs attributes have; null when it can't be read.</summary>
    /// <param name="path">Path relative to the root, e.g. "proc/stat".</param>
    public string? Text(string path)
    {
        try
        {
            return File.ReadAllText(Full(path)).TrimEnd('\0', '\n', '\r', ' ', '\t');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A file holding one integer, as most sysfs attributes do; null when missing or not a number.</summary>
    public long? Number(string path) =>
        long.TryParse(Text(path), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value) ? value : null;

    /// <summary>Names of the entries (files, folders or links) in a folder, in ordinal order; empty when the folder is missing.</summary>
    public IReadOnlyList<string> Entries(string path)
    {
        try
        {
            return [.. Directory.EnumerateFileSystemEntries(Full(path)).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>True when the file, folder or link exists.</summary>
    public bool Exists(string path) => Path.Exists(Full(path));

    private string Full(string path) => Path.Combine(root, path);
}
