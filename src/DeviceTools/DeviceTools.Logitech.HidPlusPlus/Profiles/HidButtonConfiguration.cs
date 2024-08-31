using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{DebugView}")]
public readonly struct HidButtonConfiguration
{
	public ButtonType Type { get; }
	public ButtonHidCategory HidCategory { get; }
	private readonly byte _data0;
	private readonly byte _data1;

	public MouseButtonConfiguration AsMouse() => HidCategory == ButtonHidCategory.Mouse ? Unsafe.BitCast<HidButtonConfiguration, MouseButtonConfiguration>(this) : throw new InvalidCastException();
	public KeyboardButtonConfiguration AsKeyboard() => HidCategory == ButtonHidCategory.Keyboard ? Unsafe.BitCast<HidButtonConfiguration, KeyboardButtonConfiguration>(this) : throw new InvalidCastException();
	public ConsumerControlButtonConfiguration AsConsumerControl() => HidCategory == ButtonHidCategory.ConsumerControl ? Unsafe.BitCast<HidButtonConfiguration, ConsumerControlButtonConfiguration>(this) : throw new InvalidCastException();

	public static implicit operator ButtonConfiguration(HidButtonConfiguration value) => Unsafe.BitCast<HidButtonConfiguration, ButtonConfiguration>(value);

	private object DebugView
		=> HidCategory switch
		{
			ButtonHidCategory.Mouse => AsMouse(),
			ButtonHidCategory.Keyboard => AsKeyboard(),
			ButtonHidCategory.ConsumerControl => AsConsumerControl(),
			_ => HidCategory
		};
}
