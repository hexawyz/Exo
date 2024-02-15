using System.Collections.Immutable;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;

namespace Exo.Discovery;

public sealed class MonitorDiscoveryContext : IComponentDiscoveryContext<SystemDevicePath, MonitorDriverCreationContext>
{
	private static readonly Property[] RequestedDeviceInterfaceProperties =
	[
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.InterfaceClassGuid,
		Properties.System.Devices.ContainerId,
	];

	private static readonly Property[] RequestedDeviceProperties =
	[
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
		Properties.System.Devices.Parent,
		Properties.System.Devices.Children,
		Properties.System.Devices.HardwareIds,
		Properties.System.Devices.Driver,
	];

	private readonly MonitorDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemDevicePath> DiscoveredKeys { get; }

	internal MonitorDiscoveryContext(MonitorDiscoverySubsystem discoverySubsystem, string deviceName)
	{
		_discoverySubsystem = discoverySubsystem;
		DiscoveredKeys = [deviceName];
	}

	public async ValueTask<ComponentCreationParameters<SystemDevicePath, MonitorDriverCreationContext>> PrepareForCreationAsync(CancellationToken cancellationToken)
	{
		string sourceDeviceInterfaceName = (string)DiscoveredKeys[0];

		// Try to fetch the device IDs from the device name first, as it is relatively quick and easy.
		// The format for PCI IDs is documented, so it should be relatively easy.
		// However, we might need to check the compatible IDs to get a string that can be parsed.
		bool hasDeviceId = DeviceNameParser.TryParseDeviceName(sourceDeviceInterfaceName, out var deviceId);

		// First, we must retrieve the properties of the device interface that will allow to collect related devices.
		// There can sometimes be multiple devices (e.g. HD Audio) under the container, but we are only interested in the monitor device, which should be the single relevant one.
		// If there is ever a case where we need to include other interfaces, we can revert to the behavior used for HID discovery, and return all devices & interfaces in the container.
		var deviceInterfaceProperties = await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.DeviceInterface, sourceDeviceInterfaceName, RequestedDeviceInterfaceProperties, cancellationToken).ConfigureAwait(false);

		// Retrieve the Container ID for the device.
		if (!deviceInterfaceProperties.TryGetValue(Properties.System.Devices.ContainerId.Key, out Guid containerId))
		{
			// TODO: Log ?
			throw new ArgumentOutOfRangeException("Could not resolve the container ID for the device.");
		}

		string? friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			await DeviceQuery.GetLocalizedObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false);

		// Get the device name in order to fetch the properties.
		if (!deviceInterfaceProperties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out string? sourceDeviceName))
		{
			throw new InvalidOperationException($"Could not determine the device node for device {sourceDeviceInterfaceName}.");
		}

		var deviceProperties = await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.Device, sourceDeviceName, RequestedDeviceProperties, cancellationToken).ConfigureAwait(false);

		// Read the driver value, so that we can reliably match this with EnumerateDisplayDevices.
		if (!deviceProperties.TryGetValue(Properties.System.Devices.Driver.Key, out string? driver))
		{
			throw new InvalidOperationException($"Could not resolve the driver key for {sourceDeviceName}.");
		}

		// Finish resolving the device ID in case it was not successful earlier.
		if (!hasDeviceId)
		{
			// The hardware IDs and compatible IDs should appear in a deterministic order. (See https://learn.microsoft.com/en-us/windows-hardware/drivers/install/identifiers-for-pci-devices)
			// All variants may not be able, but the IDs we are interested in should always appear first in the lists.
			// We'll first check the first hardware ID, and fallback to the first compatible ID.
			if (deviceProperties.TryGetValue(Properties.System.Devices.HardwareIds.Key, out string[]? hardwareIds) && hardwareIds.Length != 0)
			{
				DeviceId otherDeviceId;
				if (DeviceNameParser.TryParseDeviceName(hardwareIds[0], out otherDeviceId))
				{
					deviceId = otherDeviceId;
					hasDeviceId = true;
				}
			}
		}

		string displayAdapterName;
		string displayMonitorName;

		// Try to locate the device in what is returned by EnumerateDisplayDevices.
		foreach (var displayAdapter in DisplayAdapterDevice.GetAll(false))
		{
			foreach (var monitor in displayAdapter.GetMonitors(false))
			{
				if (monitor.DeviceId.EndsWith(driver))
				{
					displayMonitorName = monitor.DeviceName;
					displayAdapterName = displayAdapter.DeviceName;
					goto DisplayMonitorFound;
				}
			}
		}
		throw new InvalidOperationException("Could not match the monitor in objects returned by EnumerateDisplayDevices.");
	DisplayMonitorFound:;

		// Now, resolve the physical monitor object associated with the device.
		// To do this, we first retrieve the display configuration (covering all monitors), then we try to match it with logical and physical monitors, assuming that they should be identically ordered.
		// See https://stackoverflow.com/questions/27042576/enumdisplaydevices-vs-enumdisplaymonitors answer by Hans Passant.

		// TODO: We may want to introduce some retry logic here if we detect a mismatch between display configuration and logical/physical monitors.

		PhysicalMonitor physicalMonitor;
		string adapterDeviceInterfaceName;

		// First, build a more workable structure of the sources and targets from the display configuration.
		var targetsBySource = new List<(DisplayConfigurationPathSourceInfo Source, List<DisplayConfigurationPathTargetInfo> Targets)>();
		foreach (var path in DisplayConfiguration.GetForActivePaths().Paths)
		{
			if (targetsBySource.Count == 0 || path.SourceInfo != CollectionsMarshal.AsSpan(targetsBySource)[^1].Source)
			{
				targetsBySource.Add((path.SourceInfo, new() { path.TargetInfo }));
			}
			else
			{
				CollectionsMarshal.AsSpan(targetsBySource)[^1].Targets.Add(path.TargetInfo);
			}
		}

		var logicalMonitors = LogicalMonitor.GetAll();

		if (logicalMonitors.Length != targetsBySource.Count)
		{
			goto DisplayConfigurationMismatch;
		}

		for (int i = 0; i < logicalMonitors.Length; i++)
		{
			var logicalMonitor = logicalMonitors[i];
			if (targetsBySource[i].Source.GetDeviceName() != logicalMonitor.GetMonitorInformation().DeviceName)
			{
				goto DisplayConfigurationMismatch;
			}

			var targets = targetsBySource[i].Targets;
			var physicalMonitors = logicalMonitor.GetPhysicalMonitors();
			if (physicalMonitors.Length != targets.Count)
			{
				goto DisplayConfigurationMismatch;
			}

			for (int j = 0; j < physicalMonitors.Length; j++)
			{
				var currentPhysicalMonitor = physicalMonitors[j];
				var target = targets[j];

				var targetNameInformation = target.GetDeviceNameInformation();
				// The friendly name we got from the device container should be the same as the physical monitor description, so we can use this to help avoiding mistakes.
				// Hopefully, there is never a case where this doesn't match. But it is better to fail than to return something invalid here.
				if (currentPhysicalMonitor.Description == friendlyName && targetNameInformation.GetMonitorDeviceName() == sourceDeviceInterfaceName)
				{
					// We found the monitor. See if we need to update the device ID with the most direct source information we can have. (Device names should be derived from this, but who knows…)
					if (targetNameInformation.IsEdidValid && !hasDeviceId)
					{
						deviceId = DeviceId.ForDisplay(targetNameInformation.EdidVendorId, targetNameInformation.EdidProductId);
					}
					physicalMonitor = currentPhysicalMonitor;
					adapterDeviceInterfaceName = target.Adapter.GetDeviceName();
					// Play nice and dispose all physical monitors after the successful one. (Worst case, they would get finalized)
					for (int k = j + 1; k < physicalMonitors.Length; k++)
					{
						physicalMonitors[k].Dispose();
					}
					goto FoundPhysicalMonitor;
				}
				// If the physical monitor is not a match dispose it.
				currentPhysicalMonitor.Dispose();
			}
		}
		throw new InvalidOperationException($"Could not find the physical monitor associated with the device {sourceDeviceName}.");
	FoundPhysicalMonitor:;

		if (!hasDeviceId) throw new InvalidOperationException("Could not resolve the device ID.");

		var associatedKeys = ImmutableArray.Create<SystemDevicePath>(sourceDeviceInterfaceName, sourceDeviceName);
		var creationContext = new MonitorDriverCreationContext
		(
			_discoverySubsystem,
			associatedKeys,
			deviceId,
			containerId,
			friendlyName,
			displayAdapterName,
			displayMonitorName,
			adapterDeviceInterfaceName,
			physicalMonitor,
			[new DeviceObjectInformation(DeviceObjectKind.DeviceInterface, sourceDeviceInterfaceName, deviceInterfaceProperties)],
			[new DeviceObjectInformation(DeviceObjectKind.Device, sourceDeviceName, deviceProperties)],
			0
		);

		return new(associatedKeys, creationContext, _discoverySubsystem.ResolveFactories(deviceId));

	DisplayConfigurationMismatch:;
		throw new InvalidOperationException("Could not match the display configuration with the logical monitors. Please help improve the code if you can.");
	}
}
