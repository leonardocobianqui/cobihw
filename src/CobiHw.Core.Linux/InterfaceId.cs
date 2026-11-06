using System.Security.Cryptography;
using System.Text;

namespace CobiHw.Core.Linux;

/// <summary>
/// Stable id of a network interface on Linux, derived from its name, since Linux has no interface GUID like Windows.
/// </summary>
/// <remarks>
/// A name-based UUID (version 5, RFC 9562): the same name always gives the same id, so a device chosen in the settings is
/// found again after a reboot. Modern interface names (wlp2s0, enp3s0) are stable; the MAC address isn't a good key,
/// because NetworkManager can randomize it on Wi-Fi.
/// </remarks>
internal static class InterfaceId
{
    // Namespace of cobihw's interface ids; any fixed UUID works, as long as it never changes.
    private static readonly byte[] Namespace = Guid.Parse("4b1d6c8e-7f0a-4e52-9d3b-1c2a5e6f7a80").ToByteArray(bigEndian: true);

    /// <summary>The id of the interface with this name.</summary>
    public static Guid From(string name)
    {
        byte[] hash = SHA1.HashData([.. Namespace, .. Encoding.UTF8.GetBytes(name)]);
        Span<byte> bytes = hash.AsSpan(0, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC variant
        return new Guid(bytes, bigEndian: true);
    }
}
