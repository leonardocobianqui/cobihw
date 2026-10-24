using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CobiHw.Tests;

/// <summary>Compares the C# copies of Windows structures with the sizes and offsets of the Windows SDK headers.</summary>
/// <remarks>Each check takes the 64-bit and the 32-bit value; the x86 job of the build workflow runs the 32-bit ones.</remarks>
internal static class NativeLayout
{
    /// <summary>Asserts the size Windows sees for <typeparamref name="T"/> in this process.</summary>
    /// <param name="x64">Size in a 64-bit process.</param>
    /// <param name="x86">Size in a 32-bit process.</param>
    public static void AssertSize<T>(int x64, int x86) where T : struct
    {
        int expected = Environment.Is64BitProcess ? x64 : x86;
        Assert.Equal(expected, Marshal.SizeOf<T>());

        // Structures without references are handed to Windows straight from managed memory, so that layout must match too.
        if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Assert.Equal(expected, Unsafe.SizeOf<T>());
        }
    }

    /// <summary>Asserts where a field starts inside <typeparamref name="T"/> in this process.</summary>
    /// <param name="field">Field name in the C# structure.</param>
    /// <param name="x64">Offset in a 64-bit process.</param>
    /// <param name="x86">Offset in a 32-bit process.</param>
    public static void AssertOffset<T>(string field, int x64, int x86) where T : struct =>
        Assert.Equal(Environment.Is64BitProcess ? x64 : x86, (int)Marshal.OffsetOf<T>(field));

    /// <summary>Asserts a value that depends on the process architecture, such as a header size.</summary>
    /// <param name="actual">Value in the code.</param>
    /// <param name="x64">Expected value in a 64-bit process.</param>
    /// <param name="x86">Expected value in a 32-bit process.</param>
    public static void AssertValue(int actual, int x64, int x86) =>
        Assert.Equal(Environment.Is64BitProcess ? x64 : x86, actual);
}
