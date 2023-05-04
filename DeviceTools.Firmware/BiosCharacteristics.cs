namespace DeviceTools.Firmware;

[Flags]
public enum BiosCharacteristics : uint
{
	// TODO: Better naming, especially for the INT stuff.
	Unknown = 0x0000000000000004,
	None = 0x0000000000000008,
	IndustryStandardArchitecture = 0x0000000000000010,
	MicroChannelArchitecture = 0x0000000000000020,
	ExtendedIndustryStandardArchitecture = 0x0000000000000040,
	Pci = 0x0000000000000080,
	PcCard = 0x0000000000000100,
	PlugAndPlay = 0x0000000000000200,
	AdvancedPowerManagement = 0x0000000000000400,
	Upgradeable = 0x0000000000000800,
	Shadowing = 0x0000000000001000,
	VesaLocalBus = 0x0000000000002000,
	ExtendedSystemConfigurationData = 0x0000000000004000,
	BootFromCd = 0x0000000000008000,
	SelectableBoot = 0x0000000000010000,
	SocketedRom = 0x0000000000020000,
	BootFromPcCard = 0x0000000000040000,
	EnhancedDiskDriveSpecification = 0x0000000000080000,
	Int13JapaneseFloppyForNec9800 = 0x0000000000100000,
	Int13JapaneseFloppyForToshiba = 0x0000000000200000,
	Int13Floppy360 = 0x0000000000400000,
	Int13Floppy1200 = 0x0000000000800000,
	Int13Floppy720 = 0x0000000001000000,
	Int13Floppy2880 = 0x0000000002000000,
	Int5PrintScreen = 0x0000000004000000,
	Int9KeyboardServices8042 = 0x0000000008000000,
	Int14SerialServices = 0x0000000010000000,
	Int17PrinterServices = 0x0000000020000000,
	Int10CgaOrMonoVideoServices = 0x0000000040000000,
	NecPc98 = 0x0000000080000000,
}
