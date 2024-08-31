namespace Exo.Devices.Corsair.PowerSupplies;

internal enum CorsairPmBusCommand : byte
{
	Page = 0,
	FanCommand1 = 0x3B,
	ReadVoltageIn = 0x88,
	ReadVoltageOut = 0x8B,
	ReadIntensityOut = 0x8C,
	Temperature1 = 0x8D,
	Temperature2 = 0x8E,
	ReadFanSpeed1 = 0x90,
	OverCurrentProtectionMode = 0xD8,
	ReadPowerOut = 0x96,
	ManufacturerId = 0x99,
	ManufacturerModel = 0x9A,
	ReadGlobalPowerOut = 0xEE,
	FanMode = 0xF0,
}
