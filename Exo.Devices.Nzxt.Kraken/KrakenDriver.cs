using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Nzxt.Kraken;

public class KrakenDriver
	: Driver, IDeviceDriver<IGenericDeviceFeature>, IDeviceIdFeature//, IDeviceDriver<ISensorDeviceFeature>
{
	private const int NzxtVendorId = 0x1E71;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[ProductId(VendorIdSource.Usb, NzxtVendorId, 0x3008)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<KrakenDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string friendlyName,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (deviceInterfaces.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four device interfaces.");
		}

		if (devices.Length != 4)
		{
			throw new InvalidOperationException("Expected exactly four devices.");
		}

		string? hidDeviceInterfaceName = null;
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
				HidDevice.FromPath(hidDeviceInterfaceName);
			}
		}

		if (hidDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("One of the expected device interfaces was not found.");
		}

		var hidStream = new HidFullDuplexStream(hidDeviceInterfaceName);
		try
		{
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new KrakenDriver
				(
					logger,
					hidStream,
					productId,
					version,
					friendlyName,
					new("Kraken", topLevelDeviceName, $"{NzxtVendorId:X4}:{productId:X4}", null)
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
	private readonly ILogger<KrakenDriver> _logger;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	public override DeviceCategory DeviceCategory => DeviceCategory.Other;
	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(NzxtVendorId, _productId, _versionNumber);

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

	public KrakenDriver
	(
		ILogger<KrakenDriver> logger,
		HidFullDuplexStream stream,
		ushort productId,
		ushort versionNumber,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_logger = logger;
		_stream = stream;
		_productId = productId;
		_versionNumber = versionNumber;
		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, KrakenDriver, IDeviceIdFeature>(this);
	}

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

}
