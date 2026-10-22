using System.Diagnostics;

namespace CobiHw.Tests.Fakes;

/// <summary>Holds a transfer back to a simulated line speed.</summary>
internal static class Throttle
{
    /// <summary>Waits until <paramref name="bytes"/> would have taken at <paramref name="bitsPerSecond"/>.</summary>
    /// <param name="clock">Started when the transfer began.</param>
    /// <param name="bytes">Bytes moved so far.</param>
    /// <param name="bitsPerSecond">Simulated line speed.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    public static async Task WaitAsync(Stopwatch clock, long bytes, double bitsPerSecond, CancellationToken cancellationToken)
    {
        TimeSpan due = TimeSpan.FromSeconds(bytes * 8 / bitsPerSecond) - clock.Elapsed;

        // Short waits are skipped; the clock is absolute, so the average rate stays right.
        if (due > TimeSpan.FromMilliseconds(2))
        {
            await Task.Delay(due, cancellationToken);
        }
    }
}

/// <summary>Download body of zeros, delivered at the simulated line speed.</summary>
/// <param name="length">Body size in bytes.</param>
/// <param name="bitsPerSecond">Simulated line speed.</param>
internal sealed class ZeroStream(long length, double bitsPerSecond) : Stream
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _position;

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
    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = (int)Math.Min(count, length - _position);
        Array.Clear(buffer, offset, read);
        _position += read;
        return read;
    }

    /// <inheritdoc />
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int read = (int)Math.Min(buffer.Length, length - _position);
        buffer.Span[..read].Clear();
        _position += read;
        await Throttle.WaitAsync(_clock, _position, bitsPerSecond, cancellationToken);
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Upload sink that only counts bytes, accepting them at the simulated line speed.</summary>
/// <param name="bitsPerSecond">Simulated line speed.</param>
internal sealed class CountingStream(double bitsPerSecond) : Stream
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
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
    public override void Write(byte[] buffer, int offset, int count) => _count += count;

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _count += buffer.Length;
        await Throttle.WaitAsync(_clock, _count, bitsPerSecond, cancellationToken);
    }
}
