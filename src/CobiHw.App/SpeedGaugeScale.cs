namespace CobiHw.App;

/// <summary>Scale of the speed test gauge, like Ookla's Speedtest: evenly spaced marks from 0 to 1000 Mbps.</summary>
/// <remarks>Equal space between 0–5 and 500–1000 Mbps lets slow and fast connections both read well on the same gauge.</remarks>
internal static class SpeedGaugeScale
{
    /// <summary>Marks on the scale, in Mbps.</summary>
    public static readonly double[] Marks = [0, 5, 10, 50, 100, 250, 500, 1000];

    /// <summary>Position of a speed along the arc, 0 to 1: linear between two marks.</summary>
    /// <param name="megabits">Speed in Mbps; past the last mark it stays at the end.</param>
    public static double Fraction(double megabits)
    {
        if (!(megabits > 0))
        {
            return 0;
        }

        for (int i = 1; i < Marks.Length; i++)
        {
            if (megabits <= Marks[i])
            {
                return (i - 1 + (megabits - Marks[i - 1]) / (Marks[i] - Marks[i - 1])) / (Marks.Length - 1);
            }
        }

        return 1;
    }
}
