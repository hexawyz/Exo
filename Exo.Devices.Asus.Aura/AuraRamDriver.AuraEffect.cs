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

		CyclePulse = 6,

		Wave = 7,
		CycleWave = 8,

		Chase = 9,
		CycleChase = 10,

		WideCycleChase = 11,

		Alternate = 12,
		CycleRandomFlashes = 13,

		Dynamic = 255,
	}
}


