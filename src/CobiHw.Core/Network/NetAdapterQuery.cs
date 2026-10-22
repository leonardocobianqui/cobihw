using System.Management;
using System.Runtime.InteropServices;

namespace CobiHw.Core.Network;

/// <summary>
/// Reads the "Hidden" flag of every adapter from WMI (MSFT_NetAdapter), the same list Get-NetAdapter uses.
/// </summary>
internal static class NetAdapterQuery
{
    private const string Namespace = @"root\StandardCimv2";
    private const string Query = "SELECT InterfaceGuid, Hidden FROM MSFT_NetAdapter";

    /// <summary>Lists adapters and whether each one is hidden.</summary>
    /// <returns>The list, or <see langword="null"/> when WMI is unavailable or access is denied.</returns>
    public static IReadOnlyList<VisibleAdapter>? Read()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Namespace, Query);
            using ManagementObjectCollection results = searcher.Get();
            var adapters = new List<VisibleAdapter>();

            foreach (ManagementBaseObject result in results)
            {
                using (result)
                {
                    if (result["InterfaceGuid"] is string text && Guid.TryParse(text, out Guid id))
                    {
                        adapters.Add(new VisibleAdapter(id, result["Hidden"] is true));
                    }
                }
            }

            return adapters;
        }
        catch (Exception exception) when (exception is ManagementException or COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
