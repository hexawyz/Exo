namespace Exo.Cooling.Configuration;

[TypeId(0x55E60F25, 0x3544, 0x4E42, 0xA2, 0xE8, 0x8E, 0xCC, 0x5A, 0x0A, 0xE1, 0xE1)]
public readonly struct CoolerConfiguration
{
	public required CoolingModeConfiguration CoolingMode { get; init; }
}
