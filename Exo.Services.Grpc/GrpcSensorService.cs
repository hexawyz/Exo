using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Grpc;

internal sealed class GrpcSensorService : ISensorService
{
	private readonly SensorService _sensorService;

	public GrpcSensorService(SensorService sensorService) => _sensorService = sensorService;

	public async IAsyncEnumerable<Contracts.Ui.Settings.SensorDeviceInformation> WatchSensorDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var device in _sensorService.WatchDevicesAsync(cancellationToken))
		{
			yield return device.ToGrpc();
		}
	}

	public ValueTask<SensorDataPoint> GetLastSensorValueAsync(SensorReference sensor, CancellationToken cancellationToken) => throw new NotImplementedException();

	public IAsyncEnumerable<SensorDataPoint> WatchSensorValueAsync(SensorReference sensor, CancellationToken cancellationToken) => throw new NotImplementedException();
}
