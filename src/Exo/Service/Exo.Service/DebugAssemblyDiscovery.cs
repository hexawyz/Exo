using System.Collections.Immutable;

namespace Exo.Service;

// An hardcoded assembly discovery for development.
// When published, the application should look for plugin packages in dedicated directories.
internal sealed class DebugAssemblyDiscovery : IAssemblyDiscovery
{
	public event EventHandler? AssemblyPathsChanged { add { } remove { } }

	public ImmutableArray<string> AssemblyPaths { get; }

	public DebugAssemblyDiscovery()
	{
		const string CategoryPlaceholder = "?CATEGORY?";
		const string AssemblyNamePlaceholder = "?ASSEMBLY_NAME?";

		var assembly = typeof(DebugAssemblyDiscovery).Assembly;

		var location = assembly.Location;
		var baseName = assembly.GetName().Name;

		string separator = Path.DirectorySeparatorChar.ToString();

		string template = location.Replace("-windows" + separator, separator)
			.Replace(separator + "Service" + separator + baseName + separator, separator + CategoryPlaceholder + separator + AssemblyNamePlaceholder + separator)
			.Replace(separator + baseName + ".", separator + AssemblyNamePlaceholder + ".")[..^3] + "dll";

		var plugins = new (string Category, string AssemblyName)[]
		{
			("Discovery", @"Exo.Discovery.Hid"),
			("Discovery", @"Exo.Discovery.Pci"),
			("Discovery", @"Exo.Discovery.Monitor"),
			("Discovery", @"Exo.Discovery.System"),
			("Discovery", @"Exo.Discovery.SmBios"),
			("Discovery", @"Exo.Discovery.Cpu"),
			("Discovery", @"Exo.Discovery.DnsSd"),
			("Devices", @"Exo.Devices.Logitech"),
			("Devices", @"Exo.Devices.Gigabyte"),
			("Devices", @"Exo.Devices.Apple.Keyboard"),
			("Devices", @"Exo.Devices.Lg.Monitors"),
			("Devices", @"Exo.Devices.Monitors"),
			("Devices", @"Exo.Devices.Razer"),
			("Devices", @"Exo.Devices.Razer.Legacy"),
			("Devices", @"Exo.Devices.Eaton.Ups"),
			("Devices", @"Exo.Devices.Elgato.StreamDeck"),
			("Devices", @"Exo.Devices.Elgato.Lights"),
			("Devices", @"Exo.Devices.Intel"),
			("Devices", @"Exo.Devices.Intel.Cpu"),
			("Devices", @"Exo.Devices.NVidia"),
			("Devices", @"Exo.Devices.Asus.Aura"),
			("Devices", @"Exo.Devices.Corsair.PowerSupplies"),
			("Devices", @"Exo.Devices.Nzxt"),
			("Devices", @"Exo.Devices.Nzxt.Kraken"),
#if WITH_FAKE_DEVICES
			("Discovery", "Exo.Debug"),
#endif
		};

		AssemblyPaths = plugins.Select(p => template.Replace(CategoryPlaceholder, p.Category).Replace(AssemblyNamePlaceholder, p.AssemblyName)).ToImmutableArray();
	}
}
