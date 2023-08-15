using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Exo.Features;
using Exo.Ui.Contracts;

namespace Exo.Service.Services;

internal class GrpcDeviceService : IDeviceService
{
	private readonly DriverRegistry _driverRegistry;

	public GrpcDeviceService(DriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public async IAsyncEnumerable<WatchNotification<Ui.Contracts.DeviceInformation>> WatchDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _driverRegistry.WatchAsync(cancellationToken))
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
		if (driver.Features.GetFeature<IDeviceIdDeviceFeature>() is { } deviceIdFeature)
		{
			deviceId = deviceIdFeature.DeviceId.ToGrpc();
		}
		if (driver.Features.GetFeature<ISerialNumberDeviceFeature>() is { } serialNumberFeature)
		{
			serialNumber = serialNumberFeature.SerialNumber;
		}
		bool hasBatteryLevel = driver.Features.HasFeature<IBatteryLevelDeviceFeature>();
		return new(new ExtendedDeviceInformation { DeviceId = deviceId, SerialNumber = serialNumber, HasBatteryLevel = hasBatteryLevel });
	}
}
