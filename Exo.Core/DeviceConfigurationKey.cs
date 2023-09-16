namespace Exo;

// TODO: Make the key into a builder pattern providing priority rules for compatibility keys ?
// Whatever, the idea is to always have the main key be <Driver>:<DeviceName>, and other keys being indirections towards that key.
// So, when a non-main key is used, it will move the configuration to another main key.
// What if a device is very changing ports then ? Do we agree the configuration is updated more often than expected ?
// That's maybe where the priority mechanism can take action

/// <summary>A device configuration key contains various parameters helping resolve the configuration for a specified device.</summary>
/// <remarks>
/// <para>
/// All device should have an unique device ID on the system, but drivers can manage a composite device and hence use that ID as a recognizable key.
/// For drivers relating to native Windows devices, this should always be a device instance ID or container ID, which are guaranteed unicity on the system.
/// For drivers managing devices that are not directly recognized by Windows, this should be a string with equivalent unicity as a device instance ID. 
/// </para>
/// <para>
/// The driver key should be a string uniquely identifying the driver in order to avoid name collisions.
/// No need to be over-specific by having the exact driver type name, as it could change after a refactoring. A quick identifying key would be enough.
/// </para>
/// <para>
/// The compatible hardware ID is used to strongly identify the type of hardware of the device.
/// It indicates configuration compatibility between two device instances, and can be used to reattach configuration to a device when it moved ports or when it is replaced by an identical one.
/// Drivers that support multiple devices should generally provide the Product ID or both the Vendor ID and Product ID there, to avoid any problems.
/// This key by itself doesn't need to be that specific, but will really depend on the use case of the driver.
/// Simple drivers managing a single device, or managing the exact same set of features for all devices, can use a constant string here.
/// </para>
/// <para>
/// Some device instances can be identified with a unique ID such as a MAC address or a serial number.
/// When available, this allows tracking the device more precisely when it is moved across ports.
/// It is of highest importance that when available, the reported serial number is unique.
/// </para>
/// <para>
/// In the future, we could decide to scope the <see cref="SerialNumber"/> within <see cref="CompatibleHardwareId"/>, as it somewhat makes sense from a configuration compatibility point of view.
/// If such a change is made, it would not break any existing implementations, but it would allow drivers to return "less unique" serial numbers.
/// In any case, for now, making sure that serial numbers are unique is left to the driver implementation and its specific knowledge of the devices.
/// </para>
/// </remarks>
/// <param name="DriverKey">A string uniquely identifying the driver.</param>
/// <param name="DeviceMainId">The main device ID (or device instance ID) for the driver.</param>
/// <param name="CompatibleHardwareId">A more generic ID for the device, that can match all sufficiently similar devices.</param>
/// <param name="SerialNumber">When available, a string that can serve as a serial number to uniquely identify the physical device.</param>
public readonly record struct DeviceConfigurationKey(string DriverKey, string DeviceMainId, string CompatibleHardwareId, string? SerialNumber)
{
	public void Validate()
	{
		if (string.IsNullOrEmpty(DriverKey)) throw new InvalidOperationException("The driver key cannot be null.");
		if (string.IsNullOrEmpty(DeviceMainId)) throw new InvalidOperationException("The device main ID cannot be null.");
		if (string.IsNullOrEmpty(CompatibleHardwareId)) throw new InvalidOperationException("The compatible hardware ID cannot be null.");
	}
}
