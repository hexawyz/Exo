namespace Exo.Devices.Razer;

public enum RazerLightingEffect : byte
{
	Disabled = 0,
	Static = 1,
	Breathing = 2,
	SpectrumCycle = 3,
	Wave = 4,
	Reactive = 5,
}

public enum RazerLegacyLightingEffect : byte
{
	Disabled = 0,
	Wave = 1,
	Reactive = 2,
	Breathing = 3,
	SpectrumCycle = 4,
	Static = 6,
}

public enum RazerLedId : byte
{
	None,
	ScrollWheel,
	Dpi,
	Battery,
	Logo,
	Backlight,
	Apm,
	Macro,
	GameMode,
	WirelessConnected,
	UnderGlow,
	SideStripe,
	KeyMapRed,
	KeyMapGreen,
	KeyMapBlue,
	Dongle,
	RightIo,
	LeftIo,
	AltLogo,
	Power,
	Suspend,
	Fan,
	DonglePower,
	MousePower,
	Volume,
	Mute,
	Port1,
	Port2,
	Port3,
	Port4,
	Port5,
	Port6,
	Charging,
	FastCharging,
	FullCharging,
	IosLedArray,
	Knob,
}
