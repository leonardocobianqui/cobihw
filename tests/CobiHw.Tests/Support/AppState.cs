namespace CobiHw.Tests;

/// <summary>
/// Name of the collection every test class joins. Tests in one collection run one at a time,
/// which keeps the shared app language from changing in the middle of another test.
/// </summary>
internal static class AppState
{
    /// <summary>Collection name used with <see cref="CollectionAttribute"/>.</summary>
    public const string Name = "App state";
}
