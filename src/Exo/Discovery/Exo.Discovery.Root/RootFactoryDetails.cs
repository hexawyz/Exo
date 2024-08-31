using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0x652A7CFB, 0x0338, 0x4553, 0xAB, 0xC1, 0x0A, 0x54, 0x4B, 0x0A, 0xCB, 0xFB)]
public readonly struct RootFactoryDetails
{
	public string TypeName { get; init; }
	public Guid? TypeId { get; init; }
}
