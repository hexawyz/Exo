using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public readonly record struct Rectangle
{
	[DataMember(Order = 1)]
	public int Left { get; init; }
	[DataMember(Order = 2)]
	public int Top { get; init; }
	[DataMember(Order = 3)]
	public int Width { get; init; }
	[DataMember(Order = 4)]
	public int Height { get; init; }
}
