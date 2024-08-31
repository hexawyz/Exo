namespace Exo.Service;

[Flags]
internal enum CoolingModes
{
	None = 0x00,
	Automatic = 0x01,
	Manual = 0x02,
	HardwareControlCurve = 0x04,
}
