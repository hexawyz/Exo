using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Eaton.Ups;

public sealed class UninterruptiblePowerSupplyDriver : Driver, IDeviceDriver<IGenericDeviceFeature>
{
	private const ushort EatonVendorId = 0x0463;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[ProductId(VendorIdSource.Usb, EatonVendorId, 0xFFFF)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<UninterruptiblePowerSupplyDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string friendlyName,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		// The core device should be composed of exactly one HID interface derived from the USB interface, however, windows recognize HID UPS devices and will derive Battery and Power Meter interfaces.
		if (deviceInterfaces.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four device interfaces.");
		}

		if (devices.Length != 2)
		{
			throw new InvalidOperationException("Expected exactly two devices.");
		}

		string? hidDeviceInterfaceName = null;
		string? batteryDeviceInterfaceName = null;
		string? powerMeterDeviceInterfaceName = null;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				hidDeviceInterfaceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.Battery)
			{
				batteryDeviceInterfaceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.PowerMeter)
			{
				powerMeterDeviceInterfaceName = deviceInterface.Id;
			}
		}

		if (hidDeviceInterfaceName is null || batteryDeviceInterfaceName is null || powerMeterDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("One of the expected device interfaces was not found.");
		}

		var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
		try
		{
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new UninterruptiblePowerSupplyDriver
				(
					logger,
					hidStream,
					friendlyName,
					new("EatonUPS", topLevelDeviceName, $"{EatonVendorId:X4}:FFFF", null)
				),
				null
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly HidFullDuplexStream _stream;
	private readonly ILogger<UninterruptiblePowerSupplyDriver> _logger;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	public override DeviceCategory DeviceCategory => DeviceCategory.PowerSupply;
	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

	private UninterruptiblePowerSupplyDriver
	(
		ILogger<UninterruptiblePowerSupplyDriver> logger,
		HidFullDuplexStream stream,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_logger = logger;
		_stream = stream;
		_genericFeatures = FeatureSet.Empty<IGenericDeviceFeature>();
	}

	public override async ValueTask DisposeAsync()
	{
		await _stream.DisposeAsync().ConfigureAwait(false);
	}
}
