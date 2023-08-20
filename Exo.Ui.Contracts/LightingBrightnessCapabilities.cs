using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class LightingBrightnessCapabilities
{
	[DataMember(Order = 1)]
	public ushort MinimumBrightness { get; init; }
	[DataMember(Order = 2)]
	public ushort MaximumBrightness { get; init; }
}
