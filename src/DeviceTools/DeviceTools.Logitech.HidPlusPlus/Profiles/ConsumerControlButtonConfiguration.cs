using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools.HumanInterfaceDevices.Usages;

namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
[DebuggerDisplay("{Type}, HidCategory = {HidCategory,h}, Usage = {Usage,h}")]
public readonly struct ConsumerControlButtonConfiguration
{
	public ButtonType Type { get; }
	public ButtonHidCategory HidCategory { get; }

	private readonly byte _usage0;
	private readonly byte _usage1;

	public HidConsumerUsage Usage => (HidConsumerUsage)BigEndian.ReadUInt16(in _usage0);

	public ConsumerControlButtonConfiguration(HidConsumerUsage usage)
	{
		if (usage == 0) throw new ArgumentOutOfRangeException(nameof(usage));
		Type = ButtonType.Hid;
		HidCategory = ButtonHidCategory.ConsumerControl;
		BigEndian.Write(ref _usage0, (ushort)usage);
	}

	public static implicit operator ButtonConfiguration(ConsumerControlButtonConfiguration value) => Unsafe.BitCast<ConsumerControlButtonConfiguration, ButtonConfiguration>(value);
	public static implicit operator HidButtonConfiguration(ConsumerControlButtonConfiguration value) => Unsafe.BitCast<ConsumerControlButtonConfiguration, HidButtonConfiguration>(value);
}
