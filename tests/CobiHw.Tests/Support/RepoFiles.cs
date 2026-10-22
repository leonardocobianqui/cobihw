namespace CobiHw.Tests;

/// <summary>Finds source files of the repository, for tests that compare translations and XAML.</summary>
internal static class RepoFiles
{
    /// <summary>Repository root: the first folder above the test output that holds cobihw.slnx.</summary>
    public static string Root { get; } = FindRoot();

    /// <summary>Path of a file inside src/CobiHw.App.</summary>
    /// <param name="relativePath">Path relative to the App project folder, with forward slashes.</param>
    public static string App(string relativePath) => Path.Combine(Root, "src", "CobiHw.App", relativePath);

    private static string FindRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "cobihw.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("cobihw.slnx was not found above the test output folder.");
    }
}
