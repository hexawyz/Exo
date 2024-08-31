using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Buttons = {Buttons,h}")]
public readonly struct MouseButtonConfiguration
{
	public ButtonType Type { get; }
	public ButtonHidCategory HidCategory { get; }

	private readonly byte _buttons0;
	private readonly byte _buttons1;

	public MouseButtons Buttons => (MouseButtons)BigEndian.ReadUInt16(in _buttons0);

	public MouseButtonConfiguration(MouseButtons buttons)
	{
		if (buttons == 0) throw new ArgumentOutOfRangeException(nameof(buttons));
		Type = ButtonType.Hid;
		HidCategory = ButtonHidCategory.Mouse;
		BigEndian.Write(ref _buttons0, (ushort)buttons);
	}

	public static implicit operator ButtonConfiguration(MouseButtonConfiguration value) => Unsafe.BitCast<MouseButtonConfiguration, ButtonConfiguration>(value);
	public static implicit operator HidButtonConfiguration(MouseButtonConfiguration value) => Unsafe.BitCast<MouseButtonConfiguration, HidButtonConfiguration>(value);
}
