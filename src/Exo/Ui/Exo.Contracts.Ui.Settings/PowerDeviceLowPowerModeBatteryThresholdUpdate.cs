using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class PowerDeviceLowPowerModeBatteryThresholdUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }

	[DataMember(Order = 2, Name = nameof(BatteryThreshold))]
	private ushort RawBatteryThreshold { get; init; }
	
	public required Half BatteryThreshold
	{
		get => Unsafe.BitCast<ushort, Half>(RawBatteryThreshold);
		init => RawBatteryThreshold = Unsafe.BitCast<Half, ushort>(value);
	}
}
