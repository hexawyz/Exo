namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	[Flags]
	private enum EffectChanges : byte
	{
		None = 0,
		Effect = 1,
		Colors = 2,
		Dynamic = 128,
	}
}


