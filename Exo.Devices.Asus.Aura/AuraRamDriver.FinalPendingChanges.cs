namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	[Flags]
	private enum FinalPendingChanges : byte
	{
		None = 0,
		DynamicColors = 1,
		Commit = 2, 
	}
}


