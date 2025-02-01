using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly record struct Size
{
	[DataMember(Order = 1)]
	public int Width { get; init; }
	[DataMember(Order = 2)]
	public int Height { get; init; }
}
