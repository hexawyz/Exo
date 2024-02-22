using System.Collections.Immutable;
using System.Runtime.InteropServices;
using DeviceTools;

namespace Exo.Discovery;

public sealed class HidDiscoveryContext : IComponentDiscoveryContext<SystemDevicePath, HidDriverCreationContext>
{
	private static readonly Guid RootContainerGuid = new(0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255);

	private static readonly Property[] RequestedDeviceInterfaceProperties =
	[
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.InterfaceClassGuid,

		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,

		Properties.System.DeviceInterface.Hid.VendorId,
		Properties.System.DeviceInterface.Hid.ProductId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	];

	private static readonly Property[] RequestedDeviceProperties =
	[
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
		Properties.System.Devices.Parent,
	];

	private readonly HidDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemDevicePath> DiscoveredKeys { get; }

	internal HidDiscoveryContext(HidDiscoverySubsystem discoverySubsystem, string deviceName)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [deviceName];
	}

	public async ValueTask<ComponentCreationParameters<SystemDevicePath, HidDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		string sourceDeviceInterfaceName = (string)DiscoveredKeys[0];

		// Try to fetch the device IDs from the device name first, as it is relatively quick and easy.
		// While the format is not documented for all kinds of devices and there can be exceptions to the undocumented rules, this will be reliable for many devices.
		// (NB: Theoretically, the list of Hardware IDs and Compatible IDs of the device should be searched too, but we can always improve this later if really needed)
		DeviceIdSource deviceIdSource = DeviceIdSource.Unknown;
		VendorIdSource vendorIdSource = VendorIdSource.Unknown;
		ushort vendorId = 0;
		ushort productId = 0;
		ushort versionNumber = 0;
		if (DeviceNameParser.TryParseDeviceName(sourceDeviceInterfaceName, out var deviceId))
		{
			deviceIdSource = deviceId.Source;
			vendorIdSource = deviceId.VendorIdSource;
			vendorId = deviceId.VendorId;
			productId = deviceId.ProductId;
			versionNumber = deviceId.Version;
		}

		// First, retrieve the Container ID for the device.
		// This will allow regrouping all information that we need, and that will generally be needed for all HID drivers.
		// It would be extremely rare (and likely unsupported if we don't make sure it works) that drivers would not want to bind to the entirety of a device tree.
		if (await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, sourceDeviceInterfaceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) is not Guid containerId)
		{
			// TODO: Log ?
			throw new ArgumentOutOfRangeException("Could not resolve the container ID for the device.");
		}

		bool isRootContainer = containerId == RootContainerGuid;

		string? friendlyName = !isRootContainer ?
			await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) :
			null;

		// TODO
		if (isRootContainer) throw new NotSupportedException("Resolving HID devices in the root container is not supported yet.");

		var deviceInterfaces = await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, RequestedDeviceInterfaceProperties, Properties.System.Devices.ContainerId == containerId, cancellationToken).ConfigureAwait(false);
		var devices = await DeviceQuery.FindAllAsync(DeviceObjectKind.Device, RequestedDeviceProperties, Properties.System.Devices.ContainerId == containerId, cancellationToken).ConfigureAwait(false);

		var keys = new SystemDevicePath[deviceInterfaces.Length + devices.Length];
		string? sourceDeviceName = null;
		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];
			keys[i] = deviceInterface.Id;

			if (string.Equals(deviceInterface.Id, sourceDeviceInterfaceName, StringComparison.OrdinalIgnoreCase))
			{
				deviceInterface.Properties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out sourceDeviceName);

				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.VendorId.Key, out ushort vid) &&
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.ProductId.Key, out ushort pid))
				{
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.VersionNumber.Key, out ushort? vn);

					vendorId = vid!;
					productId = pid!;
					versionNumber = vn ?? versionNumber;

					if (vendorIdSource == VendorIdSource.Unknown)
					{
						vendorIdSource = VendorIdSource.Usb;
					}
				}
			}
		}

		var devicesById = new Dictionary<string, int>(devices.Length);
		for (int i = 0; i < devices.Length; i++)
		{
			var device = devices[i];
			keys[deviceInterfaces.Length + i] = device.Id;
			devicesById.Add(device.Id, i);
		}

		if (sourceDeviceName is null || !devicesById.TryGetValue(sourceDeviceName, out int topLevelDeviceIndex))
		{
			throw new InvalidOperationException($"Could not resolve the device node for device interface {sourceDeviceInterfaceName}.");
		}

		while (true)
		{
			var device = devices[topLevelDeviceIndex];
			if (!device.Properties.TryGetValue(Properties.System.Devices.Parent.Key, out string? parentId))
			{
				throw new InvalidOperationException($"Could not resolve the parent node for device {device.Id}.");
			}
			if (!devicesById.TryGetValue(parentId, out int parentDeviceIndex))
			{
				// Sometimes, the class GUID of the top-level device can be HID, but sometimes not.
				// The bus, however, should never be HID.
				if (device.Properties.TryGetValue(Properties.System.Devices.ClassGuid.Key, out Guid classGuid)/* && classGuid != DeviceClassGuids.Hid*/ &&
					device.Properties.TryGetValue(Properties.System.Devices.BusTypeGuid.Key, out Guid busTypeGuid) && busTypeGuid != DeviceBusTypeGuids.Hid)
				{
					if (busTypeGuid == DeviceBusTypeGuids.Usb || classGuid == DeviceClassGuids.Usb)
					{
						deviceIdSource = DeviceIdSource.Usb;
					}
					else if (classGuid == DeviceClassGuids.Bluetooth)
					{
						deviceIdSource = device.Properties.TryGetValue(Properties.System.Devices.EnumeratorName.Key, out string? enumeratorName) && enumeratorName == "BTHLE" ?
							DeviceIdSource.BluetoothLowEnergy :
							DeviceIdSource.Bluetooth;
					}
				}
				break;
			}

			topLevelDeviceIndex = parentDeviceIndex;
		}

		if (topLevelDeviceIndex < 0) throw new InvalidOperationException("Could not find the top level device.");

		var associatedKeys = ImmutableCollectionsMarshal.AsImmutableArray(keys);

		var creationContext = new HidDriverCreationContext
		(
			_discoverySubsystem,
			associatedKeys,
			new(deviceIdSource, vendorIdSource, vendorId, productId, versionNumber),
			containerId,
			friendlyName,
			ImmutableCollectionsMarshal.AsImmutableArray(deviceInterfaces),
			ImmutableCollectionsMarshal.AsImmutableArray(devices),
			topLevelDeviceIndex
		);

		return new(associatedKeys, creationContext, _discoverySubsystem.ResolveFactories(deviceId));
	}
}
