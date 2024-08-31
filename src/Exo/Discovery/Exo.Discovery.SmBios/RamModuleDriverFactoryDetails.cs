using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0xF8F7C21D, 0xDBA5, 0x4E27, 0x95, 0x53, 0xB7, 0x19, 0xA3, 0xFE, 0x7B, 0xC2)]
public readonly struct RamModuleDriverFactoryDetails
{
	public ImmutableArray<RamModuleKey> SupportedModules { get; init; }

	public RamModuleDriverFactoryDetails()
	{
		SupportedModules = [];
	}
}
