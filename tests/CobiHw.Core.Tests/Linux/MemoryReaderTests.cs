using CobiHw.Core.Linux.Readers;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Memory from /proc/meminfo.</summary>
public sealed class MemoryReaderTests
{
    [Fact]
    public void Used_IsTotalMinusAvailable_NotMinusFree()
    {
        MemorySnapshot memory = MemoryReader.Parse(Notebook.MemInfo)!;

        Assert.Equal(7380348UL * 1024, memory.TotalBytes);
        Assert.Equal((7380348UL - 2652236) * 1024, memory.UsedBytes);
    }

    [Fact]
    public void WithoutMemAvailable_TheReadingIsUnknown()
    {
        Assert.Null(MemoryReader.Parse("MemTotal: 1000 kB\nMemFree: 500 kB\n"));
        Assert.Null(MemoryReader.Parse(null));
    }
}
