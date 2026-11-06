using CobiHw.Core.Linux;

namespace CobiHw.Core.Tests.Support;

/// <summary>
/// A throwaway folder laid out like / on Linux, for the readers to read instead of the real /proc and /sys.
/// </summary>
/// <remarks>Plain files and folders only, no symlinks, so the tests also run on Windows checkouts.</remarks>
internal sealed class FakeSystem : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("cobihw-linux-").FullName;

    /// <summary>The fake root, as the readers take it.</summary>
    public SystemFiles Files => new(_root);

    /// <summary>Writes a file, creating its folders.</summary>
    /// <param name="path">Path relative to the root, e.g. "proc/stat".</param>
    /// <param name="content">Content; sysfs attributes end with a newline, as on a real system.</param>
    public FakeSystem File(string path, string content)
    {
        string full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllText(full, content);
        return this;
    }

    /// <summary>Creates a folder, e.g. the "device" link that marks real hardware.</summary>
    public FakeSystem Folder(string path)
    {
        Directory.CreateDirectory(Path.Combine(_root, path));
        return this;
    }

    /// <summary>Removes a file or folder, e.g. to unplug a disk between two samples.</summary>
    public FakeSystem Remove(string path)
    {
        string full = Path.Combine(_root, path);
        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
        else
        {
            System.IO.File.Delete(full);
        }

        return this;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder isn't worth failing a test over.
        }
    }
}
