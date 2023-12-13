using System.ComponentModel;
using System.Runtime.Serialization;
using Exo.Programming;

namespace Exo.Service;

[DataContract]
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
