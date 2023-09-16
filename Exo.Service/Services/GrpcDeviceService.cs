using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Exo.Features;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcDeviceService : IDeviceService, IAsyncDisposable
{
	private readonly DeviceRegistry _driverRegistry;
	private readonly BatteryWatcher _batteryWatcher;

	public GrpcDeviceService(DeviceRegistry driverRegistry)
	{
		_driverRegistry = driverRegistry;
		_batteryWatcher = new BatteryWatcher(driverRegistry);
	}

	public ValueTask DisposeAsync() => _batteryWatcher.DisposeAsync();

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAvailableAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new()
			{
				NotificationKind = notification.Kind.ToGrpc(),
				Details = notification.DeviceInformation.ToGrpc(),
			};
		}
	}

	public ValueTask<ExtendedDeviceInformation> GetExtendedDeviceInformationAsync(DeviceRequest request, CancellationToken cancellationToken)
	{
		if (!_driverRegistry.TryGetDriver(request.Id, out var driver))
		{
			return ValueTask.FromException<ExtendedDeviceInformation>(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException()));
		}

		string? serialNumber = null;
		DeviceId? deviceId = null;
		if (driver.Features.GetFeature<IDeviceIdFeature>() is { } deviceIdFeature)
		{
			deviceId = deviceIdFeature.DeviceId.ToGrpc();
		}
		if (driver.Features.GetFeature<ISerialNumberDeviceFeature>() is { } serialNumberFeature)
		{
			serialNumber = serialNumberFeature.SerialNumber;
		}
		bool hasBatteryState = driver.Features.HasFeature<IBatteryStateDeviceFeature>();
		return new(new ExtendedDeviceInformation { DeviceId = deviceId, SerialNumber = serialNumber, HasBatteryState = hasBatteryState });
	}

	public async IAsyncEnumerable<BatteryChangeNotification> WatchBatteryChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _batteryWatcher.WatchAsync(cancellationToken))
		{
			if (notification.NotificationKind == WatchNotificationKind.Removal) continue;

			yield return new()
			{
				DeviceId = notification.Key,
				Level = notification.NewValue.Level,
				BatteryStatus = (Ui.Contracts.BatteryStatus)notification.NewValue.BatteryStatus,
				ExternalPowerStatus = (Ui.Contracts.ExternalPowerStatus)notification.NewValue.ExternalPowerStatus
			};
		}
	}
}
