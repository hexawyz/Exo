namespace Exo.Service;

[TypeId(0x52E3D034, 0xA21B, 0x404F, 0x86, 0x31, 0xEB, 0xFC, 0x96, 0xBD, 0x26, 0xA7)]
public sealed record class DeviceUserConfiguration
{
	public required string FriendlyName { get; init; }
	// Indicates that the device configuration should not be strictly tied tied to the main device name. Does not apply if the device has a serial number.
	public bool IsAutomaticallyRemapped { get; init; }
}
