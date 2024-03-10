using Exo.SystemManagementBus;

namespace Exo.Features;

/// <summary>A feature exposed by motherboard devices, allowing access to the main SMBus.</summary>
/// <remarks>
/// I assume some complex motherboard setups could have multiple SMBuses used in different cases.
/// I don't own this kind of hardware and I have absolutely no idea how it should be dealt with in that case.
/// It seems that it is common to have buses numbered in some hardcoded way, but that isn't very helpful for us, as it is likely manufacturer-specific.
/// For example, the Z490 Vision D motherboard only has the SMBus #2 available. And it is actually hardcoded in the ACPI tables from the firmware. (I manually verified this)
/// For now, this interface should only return the SMBus used to access RAM devices. Availability of other devices on this bus is likely but not guaranteed.
/// It must be definitely be improved later, when there is a more clear idea of the actual needs. (Similarly to what is done for I2C buses per GPU and per monitor)
/// </remarks>
public interface IMotherboardSystemManagementBusFeature :
	IMotherboardDeviceFeature,
	ISystemManagementBus
{
}
