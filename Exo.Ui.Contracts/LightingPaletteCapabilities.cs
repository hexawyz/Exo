using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class LightingPaletteCapabilities
{
	[DataMember(Order = 1)]
	public byte ColorCount { get; init; }
}
