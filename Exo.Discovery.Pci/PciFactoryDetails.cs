using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0x9AC9D0D1, 0x8A56, 0x45EE, 0x8A, 0x1D, 0x6D, 0x45, 0x68, 0x86, 0x41, 0xA7)]
public readonly struct PciFactoryDetails
{
	public ImmutableArray<ProductVersionKey> ProductVersions { get; init; }
	public ImmutableArray<ProductKey> Products { get; init; }
	public ImmutableArray<VendorKey> Vendors { get; init; }
}
