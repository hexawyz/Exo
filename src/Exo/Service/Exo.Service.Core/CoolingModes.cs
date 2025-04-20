namespace Exo.Service;

[Flags]
internal enum CoolingModes : byte
{
	None = 0b00000000,
	Automatic = 0b00000001,
	Manual = 0b00000010,
	HardwareControlCurve = 0b00000100,
}
