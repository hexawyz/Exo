using System.Collections.Immutable;

namespace Exo.Service.Configuration;

// Used to persist the name of assemblies that were loaded at least once.
// Normally, the component discovery and loading system should strictly avoid unnecessarily loading assemblies,
// so all assemblies listed here will have been loaded once for a valid reason.
// This information will be used to feed the UI with a list of metadata that should be loaded for proper function.
[TypeId(0x4DD1E437, 0x1394, 0x45A4, 0xAA, 0x33, 0x3F, 0x1C, 0x45, 0xD2, 0x39, 0xC1)]
internal readonly struct UsedAssemblyDetails
{
	public readonly ImmutableArray<string> UsedAssemblyNames { get; init; }
}
