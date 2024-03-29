using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Exo.Service;

namespace Exo.Service.Grpc;

internal class GrpcDeviceService : IDeviceService, IAsyncDisposable
{
	//[DataContract]
	//[TypeId(0xC87705F2, 0x16F8, 0x4506, 0xAF, 0x14, 0x78, 0x26, 0xD7, 0xE0, 0x52, 0xE6)]
	//private record struct CachedDeviceInformation
	//{
	//}

	private readonly ConfigurationService _configurationService;
	private readonly DeviceRegistry _driverRegistry;
	private readonly BatteryWatcher _batteryWatcher;
	//private readonly ConcurrentDictionary<Guid, CachedDeviceInformation> _cachedInformation;

	public GrpcDeviceService(ConfigurationService configurationService, DeviceRegistry driverRegistry)
	{
		_configurationService = configurationService;
		_driverRegistry = driverRegistry;
		_batteryWatcher = new BatteryWatcher(driverRegistry);
		//_cachedInformation = new();
	}

	public ValueTask DisposeAsync() => _batteryWatcher.DisposeAsync();

	public async IAsyncEnumerable<WatchNotification<Contracts.Ui.Settings.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = notification.DeviceInformation.ToGrpc(),
			};
		}
	}

	//public async ValueTask<ExtendedDeviceInformation> GetExtendedDeviceInformationAsync(DeviceRequest request, CancellationToken cancellationToken)
	//{
	//	bool hasPreviousInfo = _cachedInformation.TryGetValue(request.Id, out var oldInfo);

	//	if (!_driverRegistry.TryGetDriver(request.Id, out var driver))
	//	{
	//		if (!hasPreviousInfo)
	//		{
	//			throw new InvalidOperationException("The driver for the device is not available.");
	//		}
	//		else
	//		{
	//			return new() { };
	//		}
	//	}

	//	string? serialNumber = null;
	//	if (driver.Features.GetFeature<ISerialNumberDeviceFeature>() is { } serialNumberFeature)
	//	{
	//		serialNumber = serialNumberFeature.SerialNumber;
	//	}

	//	var newInfo = new CachedDeviceInformation { SerialNumber = serialNumber };

	//	if (!hasPreviousInfo || oldInfo != newInfo)
	//	{
	//		await _configurationService.WriteDeviceConfigurationAsync(request.Id, newInfo, default).ConfigureAwait(false);
	//	}
	//	else
	//	{
	//		newInfo = oldInfo;
	//	}

	//	return new() { };
	//}

	public async IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _batteryWatcher.WatchAsync(cancellationToken))
		{
			if (notification.NotificationKind == WatchNotificationKind.Removal) continue;

			yield return new()
			{
				DeviceId = notification.Key,
				Level = notification.NewValue.Level,
				BatteryStatus = (BatteryStatus)notification.NewValue.BatteryStatus,
				ExternalPowerStatus = (ExternalPowerStatus)notification.NewValue.ExternalPowerStatus
			};
		}
	}
}
