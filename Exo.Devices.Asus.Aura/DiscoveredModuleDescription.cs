namespace Exo.Devices.Asus.Aura;

public partial class AuraRamDriver
{
	private struct DiscoveredModuleDescription
	{
		public Guid ZoneId;
		public byte Address;
		private byte _colorState;
		public AuraEffect Effect;
		public sbyte FrameDelay;
		public bool IsReversed;
		public TenColorArray Colors;

		public bool HasExtendedColors
		{
			get => (sbyte)_colorState < 0;
			set => _colorState = value ? (byte)(_colorState | 0x80) : (byte)(_colorState & 0x7F);
		}

		public byte ColorCount
		{
			get => (byte)(_colorState & 0x7F);
			set => _colorState = (byte)(_colorState & 0x80 | value & 0x7F);
		}
	}
}


