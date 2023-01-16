using System;
using System.Collections.Immutable;

namespace Exo.Service;

internal interface IAssemblyDiscovery
{
	event EventHandler? AssemblyPathsChanged;

	ImmutableArray<string> AssemblyPaths { get; }
}
