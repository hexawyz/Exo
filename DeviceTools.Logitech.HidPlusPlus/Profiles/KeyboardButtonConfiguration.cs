using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices.Usages;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Modifiers = {Modifiers,h}, Usage = {Usage,h}")]
public readonly struct KeyboardButtonConfiguration
{
	public ButtonType Type { get; }
	public ButtonHidCategory HidCategory { get; }
	public KeyboardButtonModifiers Modifiers { get; }
	private readonly byte _usage;
	public HidKeyboardUsage Usage => (HidKeyboardUsage)_usage;

	public KeyboardButtonConfiguration(KeyboardButtonModifiers modifiers, HidKeyboardUsage usage)
	{
		if ((modifiers & (KeyboardButtonModifiers.Control | KeyboardButtonModifiers.Shift)) != 0) throw new ArgumentOutOfRangeException(nameof(modifiers));
		if (usage == 0 || (byte)usage > 0xFF) throw new ArgumentOutOfRangeException(nameof(usage));
		Type = ButtonType.Hid;
		HidCategory = ButtonHidCategory.Keyboard;
		_usage = (byte)(ushort)usage;
	}

	public static implicit operator ButtonConfiguration(KeyboardButtonConfiguration value) => Unsafe.BitCast<KeyboardButtonConfiguration, ButtonConfiguration>(value);
	public static implicit operator HidButtonConfiguration(KeyboardButtonConfiguration value) => Unsafe.BitCast<KeyboardButtonConfiguration, HidButtonConfiguration>(value);
}
