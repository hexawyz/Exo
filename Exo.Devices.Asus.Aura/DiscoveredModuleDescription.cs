namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private struct DiscoveredModuleDescription
	{
		public Guid ZoneId;
		public byte Address;
		public AuraEffect Effect;
		public bool HasExtendedColors;
		public byte ColorCount;
		public TenColorArray Colors;
	}
}


