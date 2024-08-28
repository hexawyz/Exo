using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{DebugView}")]
public readonly struct ButtonConfiguration
{
	public static readonly ButtonConfiguration Disabled = new(ButtonType.Disabled, 0, 0, 0);

	public ButtonType Type { get; }
	private readonly byte _data0;
	private readonly byte _data1;
	private readonly byte _data2;

	private ButtonConfiguration(ButtonType type, byte data0, byte data1, byte data2)
		=> (Type, _data0, _data1, _data2) = (type, data0, data1, data2);

	public MacroButtonConfiguration AsMacro() => Type == ButtonType.Macro ? Unsafe.BitCast<ButtonConfiguration, MacroButtonConfiguration>(this) : throw new InvalidCastException();
	public HidButtonConfiguration AsHid() => Type == ButtonType.Hid ? Unsafe.BitCast<ButtonConfiguration, HidButtonConfiguration>(this) : throw new InvalidCastException();
	public SpecialButtonConfiguration AsSpecial() => Type == ButtonType.Special ? Unsafe.BitCast<ButtonConfiguration, SpecialButtonConfiguration>(this) : throw new InvalidCastException();

	private object DebugView
		=> Type switch
		{
			ButtonType.Macro => AsMacro(),
			ButtonType.Hid => AsHid(),
			ButtonType.Special => AsSpecial(),
			_ => Type
		};
}
