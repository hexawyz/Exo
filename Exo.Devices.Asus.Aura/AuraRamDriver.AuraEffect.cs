namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private enum AuraEffect : byte
	{
		Off = 0,
		Static = 1,
		Pulse = 2,
		Flash = 3,
		ColorCycle = 4,
		ColorWave = 5,

		Dynamic = 255,
	}
}


