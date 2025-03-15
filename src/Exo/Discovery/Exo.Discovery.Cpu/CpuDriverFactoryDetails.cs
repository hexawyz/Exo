using System.Collections.Immutable;

namespace Exo.Discovery;

[TypeId(0x96138404, 0x44A0, 0x41D9, 0xA9, 0x59, 0x51, 0x22, 0x88, 0x88, 0x37, 0xD7)]
public readonly struct CpuDriverFactoryDetails
{
	public ImmutableArray<X86VendorId> SupportedVendors { get; init; }

	public CpuDriverFactoryDetails()
	{
		SupportedVendors = [];
	}
}
