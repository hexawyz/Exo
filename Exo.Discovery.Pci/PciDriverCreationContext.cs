using System.Collections.Immutable;
using DeviceTools;
using Microsoft.Extensions.Logging;

namespace Exo.Discovery;

/// <summary>The context available for PCI device driver creation.</summary>
/// <remarks>
/// <para>All of the properties below can optionally be accessed to retrieve information on the device being initialized.</para>
/// <para>
/// While not enforced, some properties definitely must be accessed in order to properly initialize a device.
/// Not doing so would prevent the factory from knowing which device to initialize, and would de facto be an error.
/// </para>
/// <para>
/// The properties exposed here are useful enough to bootstrap drivers for PCI devices, but most drivers will need specific code to adapt to the device in use.
/// </para>
/// </remarks>
public sealed class PciDriverCreationContext : DriverCreationContext
{
	private readonly PciDiscoverySubsystem _discoverySubsystem;
	/// <summary>Gets the keys corresponding to all devices and devices interfaces that are in the container.</summary>
	/// <remarks>Most driver factories can return these keys as-is, without the need to recompute it themselves.</remarks>
	public ImmutableArray<SystemDevicePath> Keys { get; }
	/// <summary>Gets the device ID.</summary>
	public DeviceId DeviceId { get; }
	/// <summary>Gets all the device interfaces in the container.</summary>
	/// <remarks>These can easily be filtered on the HID device interface class in order to retrieve the required details.</remarks>
	public ImmutableArray<DeviceObjectInformation> DeviceInterfaces { get; }
	/// <summary>Gets all the devices in the container.</summary>
	/// <remarks>These device details are mostly useful to reconstruct and browse the hierarchy of objects.</remarks>
	public ImmutableArray<DeviceObjectInformation> Devices { get; }
	/// <summary>Gets the index of the top level device in the <see cref="Devices"/> array.</summary>
	/// <remarks>The name of the top level device is usually a good candidate to be the configuration key of a device.</remarks>
	public int TopLevelDeviceIndex { get; }
	/// <summary>Gets the of the top level device, also available through <see cref="TopLevelDeviceIndex"/>.</summary>
	public DeviceObjectInformation TopLevelDevice => Devices[TopLevelDeviceIndex];
	/// <summary>Gets the name of the top level device referenced by <see cref="TopLevelDevice"/>.</summary>
	/// <remarks>This property is the quickest way to access the information if you don't require access to the rest of device information.</remarks>
	public string TopLevelDeviceName => TopLevelDevice.Id;

	protected override INestedDriverRegistryProvider NestedDriverRegistryProvider => _discoverySubsystem.DriverRegistry;
	public override ILoggerFactory LoggerFactory => _discoverySubsystem.LoggerFactory;

	/// <summary>Gets the Vendor ID of the device.</summary>
	/// <remarks>This property is part of <see cref="DeviceId"/>. Also accessible through <see cref="DeviceId.VendorId"/>.</remarks>
	public ushort VendorId => DeviceId.VendorId;
	/// <summary>Gets the Product ID of the device.</summary>
	/// <remarks>This property is part of <see cref="DeviceId"/>. Also accessible through <see cref="DeviceId.ProductId"/>.</remarks>
	public ushort ProductId => DeviceId.ProductId;
	/// <summary>Gets the Version Number of the device.</summary>
	/// <remarks>This property is rarely useful, but it is part of <see cref="DeviceId"/>. Also accessible through <see cref="DeviceId.Version"/>.</remarks>
	public ushort Version => DeviceId.Version;

	internal PciDriverCreationContext
	(
		PciDiscoverySubsystem discoverySubsystem,
		ImmutableArray<SystemDevicePath> keys,
		DeviceId deviceId,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		int topLevelDeviceIndex
	)
	{
		_discoverySubsystem = discoverySubsystem;
		Keys = keys;
		DeviceId = deviceId;
		DeviceInterfaces = deviceInterfaces;
		Devices = devices;
		TopLevelDeviceIndex = topLevelDeviceIndex;
	}
}
