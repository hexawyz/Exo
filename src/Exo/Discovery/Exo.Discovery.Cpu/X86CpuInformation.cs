namespace Exo.Discovery;

public readonly struct X86CpuInformation
{
	public byte Index { get; init; }
	public X86VendorId VendorId { get; init; }
}
