using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcSensorService : ISensorService
{
	private readonly SensorService _sensorService;
	private readonly ILogger<GrpcSensorService> _logger;

	public GrpcSensorService(SensorService sensorService, ILogger<GrpcSensorService> logger)
	{
		_sensorService = sensorService;
		_logger = logger;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.SensorDeviceInformation> WatchSensorDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcSensorServiceDeviceWatchStart();
		try
		{
			await foreach (var device in _sensorService.WatchDevicesAsync(cancellationToken))
			{
				yield return device.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcSensorServiceDeviceWatchStop();
		}
	}
}
