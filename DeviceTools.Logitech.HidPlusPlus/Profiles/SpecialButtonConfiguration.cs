using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{Type}, Button = {Button,h}")]
public readonly struct SpecialButtonConfiguration
{
	public ButtonType Type { get; }
	public SpecialButton Button { get; }
	private readonly byte _reserved;
	public readonly byte ProfileIndex { get; }

	public SpecialButtonConfiguration(SpecialButton button)
	{
		if (button == 0) throw new ArgumentOutOfRangeException(nameof(button));
		Type = ButtonType.Special;
		Button = button;
	}

	public SpecialButtonConfiguration(SpecialButton button, byte profileIndex)
	{
		if (button == 0) throw new ArgumentOutOfRangeException(nameof(button));
		Type = ButtonType.Special;
		Button = button;
		ProfileIndex = profileIndex;
	}

	public static implicit operator ButtonConfiguration(SpecialButtonConfiguration value) => Unsafe.BitCast<SpecialButtonConfiguration, ButtonConfiguration>(value);
}
