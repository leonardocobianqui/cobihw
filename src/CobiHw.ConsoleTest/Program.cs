using CobiHw.Core;

using var monitor = new HardwareMonitor();
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

Console.CursorVisible = false;
int lastWidth = -1;

while (!cancellation.IsCancellationRequested)
{
    try
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    while (Console.KeyAvailable)
    {
        if (Console.ReadKey(intercept: true).Key == ConsoleKey.R)
        {
            monitor.ResetNetworkSession();
        }
    }

    HardwareSnapshot snapshot = monitor.Sample();
    int width = Console.WindowWidth;

    if (width != lastWidth)
    {
        Console.Clear();
        lastWidth = width;
    }

    Console.SetCursorPosition(0, 0);

    foreach (string line in Report.Build(snapshot))
    {
        Console.WriteLine(Report.FitToWidth(line, width - 1));
    }
}

Console.CursorVisible = true;

internal static class Report
{
    public static IEnumerable<string> Build(HardwareSnapshot s)
    {
        yield return "Compare com o Gerenciador de Tarefas.  [R] zera a rede  |  [Ctrl+C] sai";
        yield return string.Empty;
        yield return $"CPU    {s.Cpu.Name ?? "—"}";
        yield return $"       Uso: {Percent(s.Cpu.UsagePercent)}   Velocidade: {Ghz(s.Cpu.FrequencyGhz)}";
        yield return string.Empty;
        yield return $"RAM    Uso: {Percent(s.Memory?.UsagePercent)}   {Gigabytes(s.Memory?.UsedBytes)} de {Gigabytes(s.Memory?.TotalBytes)}";
        yield return string.Empty;
        yield return $"GPU    {s.Gpu.Name ?? "—"}";
        yield return $"       Uso: {Percent(s.Gpu.UsagePercent)}   VRAM: {Gigabytes(s.Gpu.MemoryUsedBytes)} de {Gigabytes(s.Gpu.MemoryTotalBytes)}";
        yield return string.Empty;
        yield return $"Disco  Tempo ativo: {Disks(s.Disk)}";
        yield return string.Empty;
        yield return $"Rede   Download: {Speed(s.Network.DownloadBytesPerSecond)}   Upload: {Speed(s.Network.UploadBytesPerSecond)}";
        yield return $"       Sessão: ↓ {Size(s.Network.SessionReceivedBytes)}   ↑ {Size(s.Network.SessionSentBytes)}";
    }

    public static string FitToWidth(string line, int width) =>
        line.Length > width ? line[..width] : line.PadRight(width);

    private static string Percent(double? value) => value is null ? "—" : $"{value:0}%";

    private static string Disks(DiskSnapshot disk) =>
        disk.Disks.Count == 0
            ? "—"
            : string.Join("   ", disk.Disks.Select(d => $"{DiskName(d)}: {d.ActivePercent:0}%"));

    private static string DiskName(DiskActivity disk) =>
        disk.Letters.Length == 0 ? $"Disco {disk.Number}" : $"Disco {disk.Number} ({disk.Letters})";

    private static string Ghz(double? value) => value is null ? "—" : $"{value:0.00} GHz";

    private static string Gigabytes(ulong? bytes) => bytes is null ? "—" : $"{bytes / 1024d / 1024 / 1024:0.0} GB";

    private static string Speed(double? bytesPerSecond) =>
        bytesPerSecond is null ? "—" : $"{bytesPerSecond.Value * 8 / 1_000_000:0.00} Mbps";

    private static string Size(ulong bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
