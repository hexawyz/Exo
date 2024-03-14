namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	[Flags]
	private enum EffectChanges : byte
	{
		None = 0,
		Colors = 1,
		Dynamic = 2,
		Effect = 4,
		FrameDelay = 8,
		Direction = 16,
	}
}


