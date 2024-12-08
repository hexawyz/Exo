using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.DisplayDevices;
using Exo.I2C;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Exo.Discovery;

public sealed class MonitorDiscoveryContext : IComponentDiscoveryContext<SystemDevicePath, MonitorDriverCreationContext>
{
	private static readonly Guid RootContainerId = new(0, 0, 0, 255, 255, 255, 255, 255, 255, 255, 255);

	private static readonly Property[] RequestedDeviceInterfaceProperties =
	[
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.InterfaceClassGuid,
		Properties.System.Devices.ContainerId,
	];

	private static readonly Property[] RequestedDeviceProperties =
	[
		Properties.System.ItemNameDisplay,
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
		Properties.System.Devices.Parent,
		Properties.System.Devices.Children,
		Properties.System.Devices.HardwareIds,
		Properties.System.Devices.Driver,
	];

	private readonly ILogger<MonitorDiscoveryContext> _logger;
	private readonly MonitorDiscoverySubsystem _discoverySubsystem;
	public ImmutableArray<SystemDevicePath> DiscoveredKeys { get; }

	internal MonitorDiscoveryContext(ILogger<MonitorDiscoveryContext> logger, MonitorDiscoverySubsystem discoverySubsystem, string deviceName)
	{
		_logger = logger;
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

		// Try our best to recover the device name.

		if (!deviceInterfaceProperties.TryGetValue(Properties.System.ItemNameDisplay.Key, out string? deviceFriendlyName))
		{
			deviceFriendlyName = await DeviceQuery.GetLocalizedObjectPropertyAsync(DeviceObjectKind.Device, sourceDeviceName, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false);
		}

		string? containerFriendlyName = containerId != RootContainerId ?
			await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			await DeviceQuery.GetLocalizedObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) :
			null;

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

		if (!deviceProperties.TryGetValue(Properties.System.Devices.Parent.Key, out string? displayAdapterName))
		{
			throw new InvalidOperationException($"Could not resolve the display adapter for {sourceDeviceName}.");
		}

		byte[]? cachedRawEdid;
#pragma warning disable CA1416 // Validate platform compatibility
		using (var deviceKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{sourceDeviceName}\Device Parameters")!)
		{
			cachedRawEdid = deviceKey.GetValue("EDID") as byte[];
		}
#pragma warning restore CA1416 // Validate platform compatibility

		if (cachedRawEdid is null)
		{
			throw new InvalidOperationException("Could not get the EDID information for the monitor from the Registry.");
		}

		var edid = Edid.Parse(cachedRawEdid);

		// Use a timeout for resolving the I2C Bus. Depending on time is bad design, but for now, we don't have any way to know when waiting is ok or not.
		// e.g. Propagating a signal from the Pci discovery to inform that all known adapters have been processed: If all adapters have had the chance to initialize, we can know for sure that the I2C bus is unavailable.
		II2cBus? i2cBus;
		using (var i2cTimeoutCancellationTokenSource = new CancellationTokenSource(new TimeSpan(60 * TimeSpan.TicksPerSecond)))
		using (var hybridCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, i2cTimeoutCancellationTokenSource.Token))
		{
			MonitorI2cBusResolver i2cBusResolver;
			try
			{
				i2cBusResolver = await _discoverySubsystem.I2CBusProvider.GetMonitorBusResolverAsync(displayAdapterName, hybridCancellationTokenSource.Token).ConfigureAwait(false) ??
					throw new InvalidOperationException($"Could not resolve the I2C bus for {sourceDeviceName}.");
			}
			catch (OperationCanceledException ocex) when (i2cTimeoutCancellationTokenSource.IsCancellationRequested)
			{
				throw new InvalidOperationException($"Could not resolve the I2C bus for {sourceDeviceName} in the given period of time. This is probably because there are no associated providers for your GPU.");
			}

			i2cBus = await i2cBusResolver(edid.VendorId, edid.ProductId, edid.IdSerialNumber, edid.SerialNumber, hybridCancellationTokenSource.Token).ConfigureAwait(false);
		}

		if (!hasDeviceId) throw new InvalidOperationException("Could not resolve the device ID.");

		var associatedKeys = ImmutableArray.Create<SystemDevicePath>(sourceDeviceInterfaceName, sourceDeviceName);
		var creationContext = new MonitorDriverCreationContext
		(
			_discoverySubsystem,
			associatedKeys,
			deviceId,
			containerId,
			containerFriendlyName ?? deviceFriendlyName,
			edid,
			i2cBus,
			[new DeviceObjectInformation(DeviceObjectKind.DeviceInterface, sourceDeviceInterfaceName, deviceInterfaceProperties)],
			[new DeviceObjectInformation(DeviceObjectKind.Device, sourceDeviceName, deviceProperties)],
			0
		);

		return new(associatedKeys, creationContext, _discoverySubsystem.ResolveFactories(deviceId));
	}
}
