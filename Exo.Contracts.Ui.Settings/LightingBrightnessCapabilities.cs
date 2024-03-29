using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightingBrightnessCapabilities
{
	[DataMember(Order = 1)]
	public byte MinimumBrightness { get; init; }
	[DataMember(Order = 2)]
	public byte MaximumBrightness { get; init; }
}
