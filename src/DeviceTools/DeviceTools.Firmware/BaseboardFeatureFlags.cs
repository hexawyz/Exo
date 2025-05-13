namespace DeviceTools.Firmware;

[Flags]
public enum BaseboardFeatureFlags : byte
{
	None = 0b00000000,
	HostingBoard = 0b00000001,
	DaughterBoardRequired = 0b00000010,
	Removable = 0b00000100,
	Replaceable = 0b00001000,
	HotSwappable = 0b00010000,
}
