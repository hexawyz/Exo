using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{Type}, Page = {Page,h}, Offset = {Offset,h}")]
public readonly struct MacroButtonConfiguration
{
	public ButtonType Type { get; }
	public byte Page { get; }
	private readonly byte _reserved;
	public byte Offset { get; }

	public static implicit operator ButtonConfiguration(MacroButtonConfiguration value) => Unsafe.BitCast<MacroButtonConfiguration, ButtonConfiguration>(value);
}
