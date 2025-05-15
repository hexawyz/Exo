using System.Collections.Immutable;

namespace Exo.Service.Configuration;

[TypeId(0xA1D958FA, 0x6B89, 0x45BF, 0xB2, 0xDD, 0xA2, 0x36, 0x8A, 0xCF, 0x2F, 0x26)]
internal readonly struct MenuConfiguration
{
	public required ImmutableArray<MenuItem> MenuItems { get; init; } = [];

	public MenuConfiguration() { }
}
