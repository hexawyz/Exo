namespace DeviceTools.Firmware;

[Flags]
public enum ExtendedBiosCharacteristics : ushort
{
	AdvancedConfigurationAndPowerInterface = 0x0001,
	UniversalSerialBusLegacy = 0x0002,
	AcceleratedGraphicsPort = 0x0004,
	BootFromI2O = 0x0008,
	BootFromLs120SuperDisk = 0x0010,
	BootFromAtapiZipDrive = 0x0020,
	BootFrom1394 = 0x0040,
	SmartBattery = 0x0080,

	BiosBootSpecification = 0x0100,
	FunctionKeyInitiatedNetworkBoot = 0x0200,
	TargetedContentDistribution = 0x0400,
	UnifiedExtensibleFirmwareInterfaceSpecification = 0x0800,
	IsVirtualMachine = 0x1000,
	ManufacturingModeSupported = 0x2000,
	ManufacturingModeEnabled = 0x4000,
}
