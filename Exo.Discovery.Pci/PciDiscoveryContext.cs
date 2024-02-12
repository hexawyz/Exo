using System.Collections.Immutable;
using System.Runtime.InteropServices;
using DeviceTools;

namespace Exo.Discovery;

public sealed class PciDiscoveryContext : IComponentDiscoveryContext<SystemDevicePath, PciDriverCreationContext>
{
	private static readonly Property[] RequestedDeviceInterfaceProperties =
	[
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.InterfaceClassGuid,
	];

	private static readonly Property[] RequestedDeviceProperties =
	[
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
		Properties.System.Devices.Parent,
		Properties.System.Devices.Children,
		Properties.System.Devices.HardwareIds,
		Properties.System.Devices.CompatibleIds,
		Properties.System.Devices.BusNumber,
		Properties.System.Devices.Address,
	];

	private readonly PciDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemDevicePath> DiscoveredKeys { get; }

	internal PciDiscoveryContext(PciDiscoverySubsystem discoverySubsystem, string deviceName)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [deviceName];
	}

	public async ValueTask<ComponentCreationParameters<SystemDevicePath, PciDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		string sourceDeviceInterfaceName = (string)DiscoveredKeys[0];

		// Try to fetch the device IDs from the device name first, as it is relatively quick and easy.
		// The format for PCI IDs is documented, so it should be relatively easy.
		// However, we might need to check the compatible IDs to get a string that can be parsed.
		bool hasDeviceId = DeviceNameParser.TryParseDeviceName(sourceDeviceInterfaceName, out var deviceId);

		// First, we must retrieve the properties of the device interface that will allow to collect related devices.
		// Unlike HID devices, PCI devices are likely to be contained in the ROOT container, which will not be helpful to collect all device nodes and device interfaces
		var deviceInterfaceProperties = await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.DeviceInterface, sourceDeviceInterfaceName, RequestedDeviceInterfaceProperties, cancellationToken).ConfigureAwait(false);

		// Some choices may depend on the kind of PCI device we are dealing with. We want to retrieve it anyway.
		if (!deviceInterfaceProperties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
		{
			throw new InvalidOperationException($"Could not determine the interface class GUID for device {sourceDeviceInterfaceName}.");
		}

		// We also need to get to the parent device in order to complete the graph.
		if (!deviceInterfaceProperties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out string? sourceDeviceName))
		{
			throw new InvalidOperationException($"Could not determine the device node for device {sourceDeviceInterfaceName}.");
		}

		DeviceObjectInformation[] deviceInterfaces;
		DeviceObjectInformation[] devices;
		SystemDevicePath[] keys;
		int topLevelDeviceIndex = -1;

		// The device node structure for PCI graphics adapter should be relatively simple.
		// If we forget about HD audio stuff, which is considered a separate device with different product ID, there is a single device node to which the device interface classes are attached.
		if (interfaceClassGuid == DeviceInterfaceClassGuids.DisplayAdapter || interfaceClassGuid == DeviceInterfaceClassGuids.DisplayDeviceArrival)
		{
			var deviceProperties = await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.Device, sourceDeviceName, RequestedDeviceProperties, cancellationToken).ConfigureAwait(false);
			devices = [new DeviceObjectInformation(DeviceObjectKind.Device, sourceDeviceName, deviceProperties)];

			deviceInterfaces = await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.DeviceInstanceId == sourceDeviceName, cancellationToken).ConfigureAwait(false);

			topLevelDeviceIndex = 0;
		}
		else
		{
			throw new InvalidOperationException("The device interface class is not supported yet. It might be trivial to support, but the code needs to be added.");
		}

		if (topLevelDeviceIndex < 0) throw new InvalidOperationException("Could not find the top level device.");

		// Finish resolving the device ID in case it was not successful earlier.
		var topLevelDevice = devices[topLevelDeviceIndex];
		if (!hasDeviceId || deviceId.Version == 0)
		{
			// The hardware IDs and compatible IDs should appear in a deterministic order. (See https://learn.microsoft.com/en-us/windows-hardware/drivers/install/identifiers-for-pci-devices)
			// All variants may not be able, but the IDs we are interested in should always appear first in the lists.
			// We'll first check the first hardware ID, and fallback to the first compatible ID.
			if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.HardwareIds.Key, out string[]? hardwareIds) && hardwareIds.Length != 0)
			{
				DeviceId otherDeviceId;
				if (DeviceNameParser.TryParseDeviceName(hardwareIds[0], out otherDeviceId))
				{
					deviceId = otherDeviceId;
					hasDeviceId = true;
				}
				else if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.CompatibleIds.Key, out string[]? compatibleIds) && compatibleIds.Length != 0)
				{
					if (DeviceNameParser.TryParseDeviceName(compatibleIds[0], out otherDeviceId))
					{
						deviceId = otherDeviceId;
						hasDeviceId = true;
					}
				}
			}
		}

		if (!hasDeviceId) throw new InvalidOperationException("Could not resolve the device ID.");

		keys = new SystemDevicePath[deviceInterfaces.Length + devices.Length];
		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			keys[i] = deviceInterfaces[i].Id;
		}
		for (int i = 0; i < devices.Length; i++)
		{
			keys[deviceInterfaces.Length + i] = devices[i].Id;
		}

		var associatedKeys = ImmutableCollectionsMarshal.AsImmutableArray(keys);

		var creationContext = new PciDriverCreationContext
		(
			_discoverySubsystem,
			associatedKeys,
			deviceId,
			ImmutableCollectionsMarshal.AsImmutableArray(deviceInterfaces),
			ImmutableCollectionsMarshal.AsImmutableArray(devices),
			topLevelDeviceIndex
		);

		return new(associatedKeys, creationContext, _discoverySubsystem.ResolveFactories(deviceId));
	}
}
