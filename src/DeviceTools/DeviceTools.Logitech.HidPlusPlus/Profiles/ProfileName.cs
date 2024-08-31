using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[DebuggerDisplay("{ToString()}")]
[StructLayout(LayoutKind.Sequential)]
public readonly struct ProfileName
{
#pragma warning disable IDE0044 // Add readonly modifier
	[InlineArray(48)]
	private struct ProfileNameData
	{
		private byte _element0;
	}
#pragma warning restore IDE0044 // Add readonly modifier

	private readonly ProfileNameData _value;

	public ProfileName() => ((Span<byte>)_value).Fill(0xFF);

	public ProfileName(string value) => Encoding.Unicode.GetBytes(value, _value);

	private static string? ToString(ReadOnlySpan<byte> span)
	{
		if (span.TrimStart((byte)0xFF).Length == 0) return null;

		int endIndex = MemoryMarshal.Cast<byte, ushort>(span).IndexOf((ushort)0);
		return Encoding.Unicode.GetString(endIndex >= 0 ? span[..(2 * endIndex)] : span);
	}

	public override string? ToString()
		=> ToString(_value);
}
