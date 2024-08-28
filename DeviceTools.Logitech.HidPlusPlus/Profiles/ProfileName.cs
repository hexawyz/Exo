using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[DebuggerDisplay("{ToString()}")]
[StructLayout(LayoutKind.Sequential)]
public readonly struct ProfileName
{
	[InlineArray(48)]
	private struct ProfileNameData
	{
		private byte _element0;
	}

	private readonly ProfileNameData _value;

	public ProfileName(string value) => Encoding.UTF8.GetBytes(value, _value);

	private static string? ToString(ReadOnlySpan<byte> span)
	{
		if (span.TrimStart((byte)0xFF).Length == 0) return null;

		int endIndex = span.IndexOf((byte)0);
		return Encoding.UTF8.GetString(endIndex >= 0 ? span[..endIndex] : span);
	}

	public override string? ToString()
		=> ToString(_value);
}
