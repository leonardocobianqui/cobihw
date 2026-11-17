using CobiHw.Core.Tests.Support;

namespace CobiHw.Core.Tests.Linux;

/// <summary>
/// Real readings from a notebook: Zorin OS 18 (Ubuntu 24.04 base), AMD Ryzen 5 7520U with its integrated Radeon 610M
/// (Mendocino), one NVMe disk with ext4, Wi-Fi, Tailscale and snaps. Trimmed to the lines the readers use, plus a few they must skip.
/// </summary>
internal static class Notebook
{
    public const string Stat =
        """
        cpu  405984 3182 135780 9305883 81958 0 16471 0 6781 0
        cpu0 54423 468 18312 1157844 9880 0 6147 0 990 0
        cpu1 38008 55 16876 1166138 10070 0 9758 0 829 0
        intr 39751122 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
        ctxt 55973167
        btime 1793993870
        processes 104617
        """;

    public const string MemInfo =
        """
        MemTotal:        7380348 kB
        MemFree:         1327436 kB
        MemAvailable:    2652236 kB
        Buffers:           93136 kB
        Cached:          1462872 kB
        SwapCached:       108400 kB
        """;

    public const string CpuInfo =
        """
        processor	: 0
        vendor_id	: AuthenticAMD
        model name	: AMD Ryzen 5 7520U with Radeon Graphics
        cpu MHz		: 4291.991

        processor	: 1
        vendor_id	: AuthenticAMD
        model name	: AMD Ryzen 5 7520U with Radeon Graphics
        cpu MHz		: 3086.202
        """;

    public const string DiskStats =
        """
           7       0 loop0 1052 0 151638 207 0 0 0 0 0 312 207 0 0 0 0 0 0
         259       0 nvme0n1 1763921 146649 162843078 9133505 262782 859627 27865314 5049567 0 201124 14188792 0 0 0 0 22806 5719
         259       1 nvme0n1p1 435 819 15142 207 2 0 2 1 0 30 209 0 0 0 0 0 0
         259       2 nvme0n1p2 1763385 145830 162824400 9133285 262780 859627 27865312 5049565 1 255666 14182850 0 0 0 0 0 0
        """;

    public const string MountInfo =
        """
        28 33 0:25 / /sys rw,nosuid,nodev,noexec,relatime shared:7 - sysfs sysfs rw
        32 33 0:28 / /run rw,nosuid,nodev,noexec,relatime shared:5 - tmpfs tmpfs rw,size=738036k,mode=755,inode64
        33 2 259:2 / / rw,relatime shared:1 - ext4 /dev/nvme0n1p2 rw,errors=remount-ro
        49 33 7:1 / /snap/bare/5 ro,nodev,relatime shared:22 - squashfs /dev/loop1 ro,errors=continue,threads=single
        50 33 7:0 / /snap/core22/2955 ro,nodev,relatime shared:23 - squashfs /dev/loop0 ro,errors=continue,threads=single
        64 33 259:1 / /boot/efi rw,relatime shared:34 - vfat /dev/nvme0n1p1 rw,fmask=0077,dmask=0077
        """;

    /// <summary>The battery reports charge (µAh) and current (µA), not energy and power.</summary>
    public const string BatteryFull =
        """
        DEVTYPE=power_supply
        POWER_SUPPLY_NAME=BAT0
        POWER_SUPPLY_TYPE=Battery
        POWER_SUPPLY_STATUS=Full
        POWER_SUPPLY_PRESENT=1
        POWER_SUPPLY_TECHNOLOGY=Li-ion
        POWER_SUPPLY_VOLTAGE_NOW=12964000
        POWER_SUPPLY_CURRENT_NOW=0
        POWER_SUPPLY_CHARGE_FULL_DESIGN=3550000
        POWER_SUPPLY_CHARGE_FULL=3473000
        POWER_SUPPLY_CHARGE_NOW=3473000
        POWER_SUPPLY_CAPACITY=100
        POWER_SUPPLY_CAPACITY_LEVEL=Full
        """;

    public const string Charger =
        """
        DEVTYPE=power_supply
        POWER_SUPPLY_NAME=AC0
        POWER_SUPPLY_TYPE=Mains
        POWER_SUPPLY_ONLINE=1
        """;

    /// <summary>The lines of pci.ids that name the notebook's GPU, plus neighbors the parser must skip.</summary>
    /// <remarks>Written with \t escapes: the format indents with tabs, which a raw literal would mix with its own spaces.</remarks>
    public const string PciIds =
        "# List of PCI ID's\n" +
        "1002  Advanced Micro Devices, Inc. [AMD/ATI]\n" +
        "\t1505  Mendocino [Radeon 610M]\n" +
        "\t1506  Mendocino\n" +
        "\t\t1043 282d  Radeon 610M\n" +
        "1022  Advanced Micro Devices, Inc. [AMD]\n" +
        "\t1506  Not a GPU\n" +
        "10de  NVIDIA Corporation\n" +
        "\t2520  GA106M [GeForce RTX 3060 Mobile / Max-Q]\n" +
        "8086  Intel Corporation\n" +
        "\t46a6  Alder Lake-P GT2 [Iris Xe Graphics]\n" +
        "C 03  Display controller\n" +
        "\t00  VGA compatible controller\n";

    /// <summary>One client of the GPU as /proc/&lt;pid&gt;/fdinfo shows it: a process holding it open (the desktop).</summary>
    public const string GpuClient =
        """
        pos:	0
        flags:	02104002
        drm-driver:	amdgpu
        drm-client-id:	7
        drm-pdev:	0000:03:00.0
        drm-total-gtt:	45860 KiB
        drm-resident-gtt:	412936 KiB
        drm-total-vram:	689312 KiB
        drm-resident-vram:	322236 KiB
        drm-memory-vram:	322236 KiB
        drm-memory-gtt: 	412936 KiB
        drm-engine-gfx:	302992942785 ns
        drm-engine-compute:	687136348 ns
        """;

    /// <summary>The notebook as a fake root: CPU, memory, the NVMe disk with its partitions and snap loops, and the three interfaces.</summary>
    public static FakeSystem Build()
    {
        var system = new FakeSystem()
            .File("proc/stat", Stat)
            .File("proc/meminfo", MemInfo)
            .File("proc/cpuinfo", CpuInfo)
            .File("proc/diskstats", DiskStats)
            .File("proc/self/mountinfo", MountInfo)
            .File("sys/class/power_supply/AC0/uevent", Charger)
            .File("sys/class/power_supply/BAT0/uevent", BatteryFull);

        foreach ((int core, long khz) in new[] { (0, 4292015L), (1, 4290736L), (2, 1109831L), (3, 4291771L) })
        {
            system.File($"sys/devices/system/cpu/cpu{core}/cpufreq/scaling_cur_freq", $"{khz}\n");
        }

        system.Folder("sys/devices/system/cpu/cpufreq").File("sys/devices/system/cpu/online", "0-3\n");

        Disk(system, "nvme0n1", "259:0", 500118192, ("nvme0n1p1", "259:1"), ("nvme0n1p2", "259:2"));
        system.File("sys/block/loop0/dev", "7:0\n").File("sys/block/loop0/size", "151512\n");
        system.File("sys/block/loop1/dev", "7:1\n").File("sys/block/loop1/size", "8\n");

        Interface(system, "lo", 1, "unknown", physical: false, 2448149, 2448149);
        Interface(system, "wlp2s0", 2, "up", physical: true, 2325411943, 426548643);
        Interface(system, "tailscale0", 3, "unknown", physical: false, 1990, 102051);

        // card1: on recent kernels the boot framebuffer holds card0 until amdgpu takes over. card1-eDP-1 is the screen.
        system.File("usr/share/hwdata/pci.ids", PciIds)
            .File("sys/class/drm/card1/device/vendor", "0x1002\n")
            .File("sys/class/drm/card1/device/device", "0x1506\n")
            .File("sys/class/drm/card1/device/class", "0x030000\n")
            .File("sys/class/drm/card1/device/boot_vga", "1\n")
            .File("sys/class/drm/card1/device/uevent", "DRIVER=amdgpu\nPCI_CLASS=30000\nPCI_ID=1002:1506\nPCI_SUBSYS_ID=1043:282D\nPCI_SLOT_NAME=0000:03:00.0\n")
            .File("sys/class/drm/card1/device/gpu_busy_percent", "0\n")
            .File("sys/class/drm/card1/device/mem_info_vram_total", "536870912\n")
            .File("sys/class/drm/card1/device/mem_info_vram_used", "504061952\n")
            .File("sys/class/drm/card1/device/mem_info_gtt_total", "3778732032\n")
            .File("sys/class/drm/card1/device/mem_info_gtt_used", "813862912\n")
            .File("sys/class/drm/card1/device/power/runtime_status", "active\n")
            .File("sys/class/drm/card1/device/hwmon/hwmon6/name", "amdgpu\n")
            .File("sys/class/drm/card1/device/hwmon/hwmon6/temp1_input", "57000\n")
            .File("sys/class/drm/card1/device/hwmon/hwmon6/temp1_label", "edge\n")
            .Folder("sys/class/drm/card1-eDP-1")
            .Folder("sys/class/drm/renderD128");
        return system;
    }

    /// <summary>A physical disk in sys/block, with its partitions.</summary>
    public static void Disk(FakeSystem system, string name, string number, long sectors, params (string Name, string Number)[] partitions)
    {
        system.Folder($"sys/block/{name}/device")
            .File($"sys/block/{name}/dev", $"{number}\n")
            .File($"sys/block/{name}/size", $"{sectors}\n");

        foreach ((string partition, string partitionNumber) in partitions)
        {
            system.File($"sys/block/{name}/{partition}/dev", $"{partitionNumber}\n")
                .File($"sys/block/{name}/{partition}/partition", "1\n");
        }
    }

    /// <summary>A network interface in sys/class/net.</summary>
    public static void Interface(FakeSystem system, string name, int index, string operstate, bool physical, ulong received, ulong sent)
    {
        string folder = $"sys/class/net/{name}";
        system.File($"{folder}/ifindex", $"{index}\n")
            .File($"{folder}/operstate", $"{operstate}\n")
            .File($"{folder}/statistics/rx_bytes", $"{received}\n")
            .File($"{folder}/statistics/tx_bytes", $"{sent}\n");

        if (physical)
        {
            system.Folder($"{folder}/device");
        }
    }
}
