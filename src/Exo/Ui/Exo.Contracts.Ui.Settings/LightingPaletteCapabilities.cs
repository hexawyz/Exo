using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightingPaletteCapabilities
{
	[DataMember(Order = 1)]
	public byte ColorCount { get; init; }
}
