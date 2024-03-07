using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0xD4836A5A, 0x2352, 0x4174, 0xBA, 0xCE, 0x3A, 0xF5, 0x78, 0x7F, 0x67, 0x72)]
public readonly struct MonitorFactoryDetails
{
	public bool IsRegisteredForMonitorDeviceInterfaceClass { get; init; }
	public ImmutableArray<ProductKey> Products { get; init; } = [];
	public ImmutableArray<VendorKey> Vendors { get; init; } = [];

	public MonitorFactoryDetails() { }
}
