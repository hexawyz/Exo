using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Exo.Service;

// An hardcoded assembly discovery for development.
// When published, the application should look for plugin packages in dedicated directories.
internal sealed class DebugAssemblyDiscovery : IAssemblyDiscovery
{
	public event EventHandler? AssemblyPathsChanged;

	public ImmutableArray<string> AssemblyPaths { get; }

	public DebugAssemblyDiscovery()
	{
		const string Placeholder = "?ASSEMBLY_NAME?";

		var assembly = typeof(DebugAssemblyDiscovery).Assembly;

		var location = assembly.Location;
		var baseName = assembly.GetName().Name;

		string separator = Path.DirectorySeparatorChar.ToString();

		string template = location.Replace("-windows" + separator, separator)
			.Replace(separator + baseName + separator, separator + Placeholder + separator)
			.Replace(separator + baseName + ".", separator + Placeholder + ".")[..^3] + "dll";

		var plugins = new[]
		{
			"Exo.Devices.Logitech",
			"Exo.Devices.Gigabyte",
			"Exo.Devices.Apple.Keyboard",
			"Exo.Devices.Lg.Monitors",
			"Exo.Devices.Razer",
		};

		AssemblyPaths = plugins.Select(p => template.Replace(Placeholder, p)).ToImmutableArray();
	}
}
