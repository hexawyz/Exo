using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;
using Exo.Contracts.Ui.Settings.Cooling;
using Exo.Cooling;
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
		await Task.Yield();
		yield break;
	}

	public ValueTask SetAutomaticCoolingAsync(AutomaticCoolingParameters parameters, CancellationToken cancellationToken)
		=> _coolingService.SetAutomaticPowerAsync(parameters.DeviceId, parameters.CoolerId, cancellationToken);

	public ValueTask SetFixedCoolingAsync(FixedCoolingParameters parameters, CancellationToken cancellationToken)
		=> _coolingService.SetFixedPowerAsync(parameters.DeviceId, parameters.CoolerId, parameters.Power, cancellationToken);

	public ValueTask SetSoftwareControlCurveCoolingAsync(SoftwareCurveCoolingParameters parameters, CancellationToken cancellationToken)
	{
		if (parameters.FallbackValue > 100) throw new ArgumentException("The fallback value must be less than or equal to 100%.");

		return parameters.ControlCurve.RawValue switch
		{
			UnsignedIntegerCoolingControlCurve unsignedIntegerCurve =>
				_coolingService.SetSoftwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorDeviceId,
					parameters.SensorId,
					parameters.FallbackValue,
					new InterpolatedSegmentControlCurve<ulong, byte>
					(
						ImmutableArray.CreateRange(unsignedIntegerCurve.SegmentPoints, dp => new DataPoint<ulong, byte>(dp.X, checked((byte)dp.Y))),
						unsignedIntegerCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			SignedIntegerCoolingControlCurve signedIntegerCurve =>
				_coolingService.SetSoftwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorDeviceId,
					parameters.SensorId,
					parameters.FallbackValue,
					new InterpolatedSegmentControlCurve<long, byte>
					(
						ImmutableArray.CreateRange(signedIntegerCurve.SegmentPoints, dp => new DataPoint<long, byte>(dp.X, checked((byte)dp.Y))),
						signedIntegerCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			SinglePrecisionFloatingPointCoolingControlCurve singleFloatCurve =>
				_coolingService.SetSoftwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorDeviceId,
					parameters.SensorId,
					parameters.FallbackValue,
					new InterpolatedSegmentControlCurve<float, byte>
					(
						ImmutableArray.CreateRange(singleFloatCurve.SegmentPoints, dp => new DataPoint<float, byte>(dp.X, checked((byte)dp.Y))),
						singleFloatCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			DoublePrecisionFloatingPointCoolingControlCurve doubleFloatCurve =>
				_coolingService.SetSoftwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorDeviceId,
					parameters.SensorId,
					parameters.FallbackValue,
					new InterpolatedSegmentControlCurve<double, byte>
					(
						ImmutableArray.CreateRange(doubleFloatCurve.SegmentPoints, dp => new DataPoint<double, byte>(dp.X, checked((byte)dp.Y))),
						doubleFloatCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			null => throw new ArgumentException("Missing control curve."),
			_ => throw new InvalidOperationException("Unsupported control curve data."),
		};
	}
}
