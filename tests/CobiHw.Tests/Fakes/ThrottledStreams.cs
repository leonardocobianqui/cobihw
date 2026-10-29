using System.Diagnostics;

namespace CobiHw.Tests.Fakes;

/// <summary>A simulated line in one direction: transfers running at the same time share its speed.</summary>
/// <param name="bitsPerSecond">Line speed.</param>
/// <param name="slowStart">After its first use, the line runs at 1% of its speed for this long, like a connection still speeding up.</param>
internal sealed class SimulatedLine(double bitsPerSecond, TimeSpan slowStart = default)
{
    private const double SlowStartShare = 0.01;

    // How far the schedule may fall behind the clock and still catch up. A wait can end late: on Windows, Task.Delay wakes on the
    // system timer, every 15.6 ms by default, so a 3 ms wait takes about 16 ms. Without this credit the line would lose that time on
    // every wait and run at a fraction of its speed. 50 ms covers a few late wakes; more would let an idle line send in a burst.
    private static readonly TimeSpan LateWakeCredit = TimeSpan.FromMilliseconds(50);

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _lock = new();
    // When the line finishes what it already accepted; time behind the clock counts only up to LateWakeCredit.
    private TimeSpan _busyUntil;
    private TimeSpan? _firstUse;

    /// <summary>Waits until <paramref name="bytes"/> would have crossed the line, after everything queued before them.</summary>
    /// <param name="bytes">Bytes to move.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public async Task MoveAsync(int bytes, CancellationToken cancellationToken)
    {
        TimeSpan due;
        lock (_lock)
        {
            TimeSpan now = _clock.Elapsed;
            _firstUse ??= now;
            // The bytes cross when the line gets to them; the speed at that moment decides how long they take.
            TimeSpan earliest = now - LateWakeCredit;
            TimeSpan start = _busyUntil > earliest ? _busyUntil : earliest;
            double speed = slowStart > TimeSpan.Zero && start - _firstUse < slowStart ? bitsPerSecond * SlowStartShare : bitsPerSecond;
            _busyUntil = start + TimeSpan.FromSeconds(bytes * 8 / speed);
            due = _busyUntil - now;
        }

        // Short waits are skipped, and waits that end late are made up by the next transfers: the schedule is absolute, so the average rate stays right.
        if (due > TimeSpan.FromMilliseconds(2))
        {
            await Task.Delay(due, cancellationToken);
        }
    }
}

/// <summary>Download body of zeros, delivered at the line speed.</summary>
/// <param name="length">Body size in bytes.</param>
/// <param name="line">Line shared with the other downloads.</param>
/// <param name="ended">Called once, when the body is read to the end, fails or is disposed.</param>
internal sealed class ZeroStream(long length, SimulatedLine line, Action ended) : Stream
{
    private long _position;
    private bool _ended;

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => length;

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = (int)Math.Min(buffer.Length, length - _position);
        if (read == 0)
        {
            End();
            return 0;
        }

        buffer.Span[..read].Clear();
        try
        {
            await line.MoveAsync(read, cancellationToken);
        }
        catch
        {
            End();
            throw;
        }

        _position += read;
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        End();
        base.Dispose(disposing);
    }

    private void End()
    {
        if (!_ended)
        {
            _ended = true;
            ended();
        }
    }
}

/// <summary>Upload sink that only counts bytes, accepting them at the line speed.</summary>
/// <param name="line">Line shared with the other uploads.</param>
internal sealed class CountingStream(SimulatedLine line) : Stream
{
    private long _count;

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => true;

    /// <summary>Bytes received so far.</summary>
    public override long Length => _count;

    /// <inheritdoc />
    public override long Position
    {
        get => _count;
        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await line.MoveAsync(buffer.Length, cancellationToken);
        _count += buffer.Length;
    }
}
