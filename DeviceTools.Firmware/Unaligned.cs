using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware;

internal static class Unaligned
{
	public static T ReadAt<T>(in T source) where T : unmanaged => Unsafe.ReadUnaligned<T>(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(source)));
	public static T Read<T>(ReadOnlySpan<byte> span) where T : unmanaged => Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span[..Unsafe.SizeOf<T>()]));
}
