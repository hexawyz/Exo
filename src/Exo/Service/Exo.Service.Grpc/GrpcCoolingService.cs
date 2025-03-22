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
	private readonly ILogger<GrpcCoolingService> _logger;

	public GrpcCoolingService(CoolingService coolingService, ILogger<GrpcCoolingService> logger)
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

	public async IAsyncEnumerable<CoolingParameters> WatchCoolingChangesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		_logger.GrpcCoolingServiceChangeWatchStart();
		try
		{
			await foreach (var update in _coolingService.WatchCoolingChangesAsync(cancellationToken))
			{
				yield return update.ToGrpc();
			}
		}
		finally
		{
			_logger.GrpcCoolingServiceChangeWatchStop();
		}
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

	public ValueTask SetHardwareControlCurveCoolingAsync(HardwareCurveCoolingParameters parameters, CancellationToken cancellationToken)
	{
		return parameters.ControlCurve.RawValue switch
		{
			UnsignedIntegerCoolingControlCurve unsignedIntegerCurve =>
				_coolingService.SetHardwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorId,
					new InterpolatedSegmentControlCurve<ulong, byte>
					(
						ImmutableArray.CreateRange(unsignedIntegerCurve.SegmentPoints, dp => new DataPoint<ulong, byte>(dp.X, checked((byte)dp.Y))),
						unsignedIntegerCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			SignedIntegerCoolingControlCurve signedIntegerCurve =>
				_coolingService.SetHardwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorId,
					new InterpolatedSegmentControlCurve<long, byte>
					(
						ImmutableArray.CreateRange(signedIntegerCurve.SegmentPoints, dp => new DataPoint<long, byte>(dp.X, checked((byte)dp.Y))),
						signedIntegerCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			SinglePrecisionFloatingPointCoolingControlCurve singleFloatCurve =>
				_coolingService.SetHardwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorId,
					new InterpolatedSegmentControlCurve<float, byte>
					(
						ImmutableArray.CreateRange(singleFloatCurve.SegmentPoints, dp => new DataPoint<float, byte>(dp.X, checked((byte)dp.Y))),
						singleFloatCurve.InitialValue,
						MonotonicityValidators<byte>.IncreasingUpTo100
					),
					cancellationToken
				),
			DoublePrecisionFloatingPointCoolingControlCurve doubleFloatCurve =>
				_coolingService.SetHardwareControlCurveAsync
				(
					parameters.CoolingDeviceId,
					parameters.CoolerId,
					parameters.SensorId,
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
