using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Programming;

namespace Exo.Service.Events;

[DataContract]
[TypeId(0x0F44EDC7, 0x5DA3, 0x48EE, 0x9F, 0x96, 0x52, 0x9E, 0x9A, 0xB8, 0xAA, 0x45)]
public class MouseDpiEventParameters : DeviceEventParameters
{
	public MouseDpiEventParameters
	(
		DeviceId deviceId,
		ushort horizontal,
		ushort vertical,
		byte levelCount,
		byte? currentLevel
	) : base(deviceId)
	{
		Horizontal = horizontal;
		Vertical = vertical;
		LevelCount = levelCount;
		CurrentLevel = currentLevel;
	}

	[DataMember(Order = 2)]
	[Description("The horizontal DPI.")]
	public ushort Horizontal { get; }
	[DataMember(Order = 3)]
	[Description("The vertical DPI.")]
	public ushort Vertical { get; }

	[DataMember(Order = 4)]
	[Description("The number of predefined DPI levels supported by the device.")]
	public byte LevelCount { get; }
	[DataMember(Order = 5)]
	[Description("The current predefined DPI level, if supported by the device.")]
	public byte? CurrentLevel { get; }
}
