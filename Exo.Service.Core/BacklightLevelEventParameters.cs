using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Programming;

namespace Exo.Service;

[DataContract]
[TypeId(0xE8607AF5, 0x9DC6, 0x4A21, 0xA5, 0xFF, 0x0B, 0xAE, 0x45, 0x4A, 0xAA, 0x0F)]
public class BacklightLevelEventParameters : DeviceEventParameters
{
	public BacklightLevelEventParameters(DeviceId deviceId, byte level, byte maximumLevel) : base(deviceId)
	{
		Level = level;
		MaximumLevel = maximumLevel;
	}

	[DataMember(Order = 2)]
	[Description("The backlight level.")]
	public byte Level { get; }
	[DataMember(Order = 3)]
	[Description("The maximum backlight level.")]
	public byte MaximumLevel { get; }
}
