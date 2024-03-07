using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0x20B354B1, 0xD4F7, 0x49F3, 0xB7, 0xD8, 0x06, 0xF1, 0xAB, 0x7F, 0x8F, 0xFA)]
public readonly struct HidFactoryDetails
{
	public ImmutableArray<ProductVersionKey> ProductVersions { get; init; }
	public ImmutableArray<ProductKey> Products { get; init; }
	public ImmutableArray<VendorKey> Vendors { get; init; }
}
