using CobiHw.Core.Network;

namespace CobiHw.Tests.Fakes;

/// <summary>
/// Collects progress reports right away. Unlike <see cref="Progress{T}"/>, it doesn't post to a
/// synchronization context, so no report is still pending when the test checks the list.
/// </summary>
internal sealed class InlineProgress : IProgress<SpeedTestProgress>
{
    private readonly List<SpeedTestProgress> _reports = [];

    /// <summary>Copy of the reports received so far.</summary>
    public IReadOnlyList<SpeedTestProgress> Reports
    {
        get
        {
            lock (_reports)
            {
                return [.. _reports];
            }
        }
    }

    /// <inheritdoc />
    public void Report(SpeedTestProgress value)
    {
        lock (_reports)
        {
            _reports.Add(value);
        }
    }
}
