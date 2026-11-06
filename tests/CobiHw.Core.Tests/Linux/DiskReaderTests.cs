using System.Diagnostics;
using CobiHw.Core.Linux.Readers;
using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>Physical disks, what each holds and their activity.</summary>
public sealed class DiskReaderTests
{
    private const long Start = 1_000;

    private static long Seconds(double seconds) => Start + (long)(seconds * Stopwatch.Frequency);

    [Fact]
    public void Notebook_OneNvmeDisk_HoldsTheSystem_LoopsAndTheEfiPartitionAreLeftOut()
    {
        using FakeSystem system = Notebook.Build();

        DiskActivity disk = Assert.Single(new DiskReader(system.Files).Read(Start).Disks);

        Assert.Equal(0, disk.Number);
        Assert.Equal("/", disk.Volumes);
        Assert.True(disk.IsSystem);
        Assert.Equal(0, disk.ActivePercent);
        Assert.Null(disk.ReadBytesPerSecond);
    }

    [Fact]
    public void SecondRead_ActiveTimeFromIoTicks_SpeedsIn512ByteSectors()
    {
        using FakeSystem system = Notebook.Build();
        var reader = new DiskReader(system.Files);
        reader.Read(Start);

        // Over 2 s: 500 ms with I/O in flight, 4096 sectors read and 2048 written.
        system.File("proc/diskstats", " 259       0 nvme0n1 1763921 146649 162847174 9133505 262782 859627 27867362 5049567 0 201624 14188792 0 0 0 0 22806 5719\n");
        DiskActivity disk = reader.Read(Seconds(2)).Disks[0];

        Assert.Equal(25, disk.ActivePercent, 6);
        Assert.Equal(4096 * 512 / 2.0, disk.ReadBytesPerSecond!.Value, 6);
        Assert.Equal(2048 * 512 / 2.0, disk.WriteBytesPerSecond!.Value, 6);
    }

    [Fact]
    public void Disks_AreNumberedByKernelName_OpticalAndEmptyReadersAreLeftOut_RemovableMountsAreShown()
    {
        using FakeSystem system = new FakeSystem()
            .File("proc/self/mountinfo",
                """
                33 2 8:2 / / rw,relatime shared:1 - ext4 /dev/sda2 rw
                40 33 8:3 / /home rw,relatime shared:2 - ext4 /dev/sda3 rw
                41 33 8:1 / /boot/efi rw,relatime shared:3 - vfat /dev/sda1 rw
                90 33 8:17 / /media/ana/My\040Files rw,nosuid,nodev,relatime shared:9 - exfat /dev/sdb1 rw
                """);
        Notebook.Disk(system, "sda", "8:0", 1000, ("sda1", "8:1"), ("sda2", "8:2"), ("sda3", "8:3"));
        Notebook.Disk(system, "sdb", "8:16", 2000, ("sdb1", "8:17"));
        Notebook.Disk(system, "sdc", "8:32", 0);
        Notebook.Disk(system, "nvme0n1", "259:0", 3000);
        Notebook.Disk(system, "sr0", "11:0", 4000);

        IReadOnlyList<DiskActivity> disks = new DiskReader(system.Files).Read(Start).Disks;

        // nvme0n1, sda, sdb: the empty card reader (sdc) and the optical drive (sr0) don't count.
        Assert.Equal(new[] { 0, 1, 2 }, disks.Select(disk => disk.Number));
        Assert.Equal(new[] { "", "/ /home", "/media/ana/My Files" }, disks.Select(disk => disk.Volumes));
        Assert.Equal(new[] { false, true, false }, disks.Select(disk => disk.IsSystem));
    }

    [Fact]
    public void Btrfs_IsFoundByItsSource_AndAnEncryptedRootLeadsToItsDisk()
    {
        // Fedora: btrfs subvolumes report a device number of their own (0:35), inside LUKS on nvme0n1p3.
        using FakeSystem system = new FakeSystem()
            .File("proc/self/mountinfo",
                """
                60 1 0:35 /root / rw,relatime shared:1 - btrfs /dev/mapper/luks-5f1c rw,subvol=/root
                61 60 0:35 /home /home rw,relatime shared:2 - btrfs /dev/mapper/luks-5f1c rw,subvol=/home
                62 60 259:2 / /boot rw,relatime shared:3 - ext4 /dev/nvme0n1p2 rw
                """)
            .File("sys/block/dm-0/dev", "253:0\n")
            .File("sys/block/dm-0/size", "1000\n")
            .File("sys/block/dm-0/dm/name", "luks-5f1c\n")
            .Folder("sys/block/dm-0/slaves/nvme0n1p3");
        Notebook.Disk(system, "nvme0n1", "259:0", 3000, ("nvme0n1p1", "259:1"), ("nvme0n1p2", "259:2"), ("nvme0n1p3", "259:3"));

        DiskActivity disk = Assert.Single(new DiskReader(system.Files).Read(Start).Disks);

        Assert.Equal("/ /home", disk.Volumes);
        Assert.True(disk.IsSystem);
    }

    [Fact]
    public void Lvm_OnTwoDisks_ShowsTheMountOnBoth()
    {
        using FakeSystem system = new FakeSystem()
            .File("proc/self/mountinfo", "33 2 253:0 / / rw,relatime shared:1 - ext4 /dev/mapper/vg-root rw\n")
            .File("sys/block/dm-0/dev", "253:0\n")
            .File("sys/block/dm-0/dm/name", "vg-root\n")
            .Folder("sys/block/dm-0/slaves/sda1")
            .Folder("sys/block/dm-0/slaves/sdb1");
        Notebook.Disk(system, "sda", "8:0", 1000, ("sda1", "8:1"));
        Notebook.Disk(system, "sdb", "8:16", 1000, ("sdb1", "8:17"));

        IReadOnlyList<DiskActivity> disks = new DiskReader(system.Files).Read(Start).Disks;

        Assert.All(disks, disk => Assert.Equal("/", disk.Volumes));
        Assert.All(disks, disk => Assert.True(disk.IsSystem));
    }

    [Fact]
    public void DiskStats_SkipsShortLines()
    {
        Dictionary<string, DiskCounters> counters = DiskReader.ParseDiskStats(Notebook.DiskStats + "\n 1 2 broken 3\n");

        Assert.Equal(new DiskCounters(162843078, 27865314, 201124), counters["nvme0n1"]);
        Assert.False(counters.ContainsKey("broken"));
    }
}
