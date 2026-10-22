using CobiHw.Core;

// Developer tool: prints every reading once a second to compare with Task Manager. Not shipped with the app.
using var monitor = new HardwareMonitor();
using var cancellation = new CancellationTokenSource();

// Ctrl+C stops the loop cleanly instead of killing the process, so the cursor is restored.
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

    // Redraws in place from the top; a full clear only when the window width changes, to avoid flicker.
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

/// <summary>
/// Formats a snapshot as plain text lines for the console test.
/// </summary>
internal static class Report
{
    /// <summary>One line per value, grouped like the Task Manager performance tab.</summary>
    public static IEnumerable<string> Build(HardwareSnapshot s)
    {
        yield return "Compare com o Gerenciador de Tarefas.  [R] zera a rede  |  [Ctrl+C] sai";
        yield return string.Empty;
        yield return $"CPU    {s.Cpu.Name ?? "—"}";
        yield return $"       Uso: {Percent(s.Cpu.UsagePercent)}   Velocidade: {Ghz(s.Cpu.FrequencyGhz)}";
        yield return string.Empty;
        yield return $"RAM    Uso: {Percent(s.Memory?.UsagePercent)}   {Gigabytes(s.Memory?.UsedBytes)} de {Gigabytes(s.Memory?.TotalBytes)}";
        yield return string.Empty;
        foreach (string line in Gpus(s.Gpu))
        {
            yield return line;
        }

        yield return string.Empty;
        yield return $"Bateria {Battery(s.Battery)}";
        yield return string.Empty;
        yield return $"Disco  Tempo ativo: {Disks(s.Disk)}";
        yield return $"       Leitura / escrita: {DiskRates(s.Disk)}";
        yield return string.Empty;
        yield return $"Rede   Download: {Speed(s.Network.DownloadBytesPerSecond)}   Upload: {Speed(s.Network.UploadBytesPerSecond)}";
        yield return $"       Sessão: ↓ {Size(s.Network.SessionReceivedBytes)}   ↑ {Size(s.Network.SessionSentBytes)}";
    }

    /// <summary>Every GPU with its type, Windows order, usage, memory and temperature.</summary>
    private static IEnumerable<string> Gpus(GpuSnapshot gpus)
    {
        if (gpus.Gpus.Count == 0)
        {
            yield return "GPU    —";
            yield break;
        }

        foreach (GpuActivity gpu in gpus.Gpus.OrderBy(gpu => gpu.Rank))
        {
            string kind = gpu.IsIntegrated ? "integrada" : "dedicada";
            string memory = gpu.IsIntegrated
                ? $"Dedicada: {Gigabytes(gpu.DedicatedUsedBytes)} de {Gigabytes(gpu.DedicatedTotalBytes)}   Compartilhada: {Gigabytes(gpu.SharedUsedBytes)}"
                : $"VRAM: {Gigabytes(gpu.DedicatedUsedBytes)} de {Gigabytes(gpu.DedicatedTotalBytes)}";
            yield return $"GPU    {gpu.Name} ({kind}, ordem {gpu.Rank + 1})";
            yield return $"       Uso: {Percent(gpu.UsagePercent)}   {memory}   Temperatura: {Celsius(gpu.TemperatureCelsius)}";
        }
    }

    /// <summary>Charge, state and remaining time, or "sem bateria".</summary>
    private static string Battery(BatterySnapshot? battery) =>
        battery is null
            ? "sem bateria"
            : $"{(battery.Percent is int percent ? $"{percent}%" : "—")}   {(battery.IsCharging ? "carregando" : battery.OnPower ? "na tomada" : "na bateria")}   Restante: {(battery.Remaining is TimeSpan left ? $"{(int)left.TotalHours} h {left.Minutes:00} min" : "—")}";

    /// <summary>Read/write speed of every disk.</summary>
    private static string DiskRates(DiskSnapshot disk) =>
        disk.Disks.Count == 0
            ? "—"
            : string.Join("   ", disk.Disks.Select(d => $"{DiskName(d)}: L {Rate(d.ReadBytesPerSecond)} / E {Rate(d.WriteBytesPerSecond)}"));

    private static string Rate(double? bytesPerSecond) => bytesPerSecond is null ? "—" : $"{bytesPerSecond.Value / 1024 / 1024:0.0} MB/s";

    private static string Celsius(double? value) => value is null ? "—" : $"{value:0.0} °C";

    /// <summary>Cuts or pads the line to the window width, so leftovers from the previous frame are overwritten.</summary>
    public static string FitToWidth(string line, int width) =>
        line.Length > width ? line[..width] : line.PadRight(width);

    private static string Percent(double? value) => value is null ? "—" : $"{value:0}%";

    /// <summary>Active time of every disk.</summary>
    private static string Disks(DiskSnapshot disk) =>
        disk.Disks.Count == 0
            ? "—"
            : string.Join("   ", disk.Disks.Select(d => $"{DiskName(d)}{(d.IsSystem ? " [Windows]" : string.Empty)}: {d.ActivePercent:0}%"));

    /// <summary>"Disco 0 (C:)", or just the number when the disk has no drive letters.</summary>
    private static string DiskName(DiskActivity disk) =>
        disk.Letters.Length == 0 ? $"Disco {disk.Number}" : $"Disco {disk.Number} ({disk.Letters})";

    private static string Ghz(double? value) => value is null ? "—" : $"{value:0.00} GHz";

    private static string Gigabytes(ulong? bytes) => bytes is null ? "—" : $"{bytes / 1024d / 1024 / 1024:0.0} GB";

    /// <summary>Network speed in Mbps.</summary>
    private static string Speed(double? bytesPerSecond) =>
        bytesPerSecond is null ? "—" : $"{bytesPerSecond.Value * 8 / 1_000_000:0.00} Mbps";

    /// <summary>Byte count with the largest fitting unit.</summary>
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
