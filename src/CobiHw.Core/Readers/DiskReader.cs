using System.Globalization;

namespace CobiHw.Core.Readers;

internal sealed class DiskReader
{
    private const string TotalInstance = "_Total";

    private readonly PdhCounter? _idleTime;

    public DiskReader(PdhQuery query)
    {
        _idleTime = query.TryAddCounter(@"\PhysicalDisk(*)\% Idle Time");
    }

    public DiskSnapshot Read() => new(ReadDisks());

    private List<DiskActivity> ReadDisks()
    {
        var disks = new List<DiskActivity>();

        if (_idleTime is null)
        {
            return disks;
        }

        foreach (var (instance, idlePercent) in _idleTime.GetInstanceValues())
        {
            if (instance == TotalInstance || !TryParseInstance(instance, out int number, out string letters))
            {
                continue;
            }

            double activePercent = Math.Clamp(100 - idlePercent, 0, 100);
            disks.Add(new DiskActivity(number, letters, activePercent));
        }

        disks.Sort((left, right) => left.Number.CompareTo(right.Number));
        return disks;
    }

    private static bool TryParseInstance(string instance, out int number, out string letters)
    {
        int separator = instance.IndexOf(' ');
        string numberText = separator < 0 ? instance : instance[..separator];
        letters = separator < 0 ? string.Empty : instance[(separator + 1)..].Trim();

        return int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
