namespace DeviceTools.Logitech.HidPlusPlus.Profiles;

#pragma warning disable IDE0044 // Add readonly modifier
public struct Profile
{
	public byte ReportRate;
	public byte DefaultDpiIndex;
	public byte SwitchedDpiIndex;
	public ProfileDpiCollection DpiPresets;
	public Color ProfileColor;
	public byte PowerMode;
	public byte AngleSnapping;
	private byte _reserved0;
	private byte _reserved1;
	private byte _reserved2;
	private byte _reserved3;
	private byte _reserved4;
	private byte _reserved5;
	private byte _reserved6;
	private byte _reserved7;
	private byte _reserved8;
	private byte _reserved9;
	private byte _lowPowerModeDelay0;
	private byte _lowPowerModeDelay1;
	private byte _powerOffDelay0;
	private byte _powerOffDelay1;
	public ButtonConfigurationCollection Buttons;
	public ButtonConfigurationCollection AlternateButtons;
	public ProfileName Name;
	public LedEffectCollection Leds;
	public LedEffectCollection AlternateLeds;
	private byte _reservedA;
	private byte _reservedB;

	public ushort LowPowerModeDelay
	{
		get => BigEndian.ReadUInt16(in _lowPowerModeDelay0);
		set => BigEndian.Write(ref _lowPowerModeDelay0, value);
	}

	public ushort PowerOffDelay
	{
		get => BigEndian.ReadUInt16(in _powerOffDelay0);
		set => BigEndian.Write(ref _powerOffDelay0, value);
	}
}
#pragma warning restore IDE0044 // Add readonly modifier
