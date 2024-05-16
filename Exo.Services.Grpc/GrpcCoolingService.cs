using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Exo.Contracts.Ui.Settings.Cooling;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcCoolingService : ICoolingService
{
	private readonly CoolingService _coolingService;
	private readonly ILogger<GrpcSensorService> _logger;

	public GrpcCoolingService(CoolingService coolingService, ILogger<GrpcSensorService> logger)
	{
		_coolingService = coolingService;
		_logger = logger;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.CoolingDeviceInformation> WatchCoolingDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcCoolingServiceDeviceWatchStart();
		try
		{
			await foreach (var device in _coolingService.WatchDevicesAsync(cancellationToken))
			{
				yield return device.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcCoolingServiceDeviceWatchStop();
		}
	}

	public async IAsyncEnumerable<CoolingParameters> WatchCoolingChangesAsync(SoftwareCurveCoolingParameters parameters, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		yield break;
	}

	public ValueTask SetAutomaticCoolingAsync(AutomaticCoolingParameters parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
	public ValueTask SetFixedCoolingAsync(FixedCoolingParameters parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
	public ValueTask SetSoftwareControlCurveCoolingAsync(SoftwareCurveCoolingParameters parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
}
