using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Exo.Service.Grpc;

internal sealed class GrpcLightService : ILightService
{
	private readonly ILogger<GrpcLightService> _logger;
	private readonly LightService _lightService;

	public GrpcLightService(ILogger<GrpcLightService> logger, LightService lightService)
	{
		_logger = logger;
		_lightService = lightService;
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.LightDeviceInformation> WatchLightDevicesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcSpecializedDeviceServiceWatchStart(GrpcService.Light);
		try
		{
			await foreach (var notification in _lightService.WatchDevicesAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return notification.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcSpecializedDeviceServiceWatchStop(GrpcService.Light);
		}
	}

	public async IAsyncEnumerable<Contracts.Ui.Settings.LightChangeNotification> WatchLightChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		// TODO: Logging
		//_logger.GrpcSpecializedDeviceServiceWatchStart(GrpcService.Light);
		try
		{
			await foreach (var configuration in _lightService.WatchLightChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return configuration.ToGrpc();
			}
		}
		finally
		{
			//_logger.GrpcSpecializedDeviceServiceWatchStop(GrpcService.Light);
		}
	}

	public async ValueTask SwitchLightAsync(LightSwitchRequest request, CancellationToken cancellationToken)
	{
		try
		{
			await _lightService.SwitchLightAsync(request.DeviceId, request.LightId, request.IsOn, cancellationToken).ConfigureAwait(false);
		}
		// TODO: Add an exception for "not found" so that we can report that status
		catch (ArgumentException ex)
		{
			throw new RpcException(new(StatusCode.InvalidArgument, ex.Message));
		}
		catch (Exception ex)
		{
			throw new RpcException(new(StatusCode.Unknown, ex.Message));
		}
	}
}
