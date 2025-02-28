using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0xE16C9BD2, 0x18F9, 0x4A2B, 0xA9, 0x2E, 0xA3, 0xFA, 0x53, 0x9D, 0x12, 0x1A)]
public readonly struct DnsSdFactoryDetails
{
	public ImmutableArray<string> ServiceTypes { get; init; } = [];

	public DnsSdFactoryDetails() { }
}
