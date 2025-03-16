using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Cooling;
using Exo.Cooling.Configuration;

namespace Exo.Service;

internal partial class CoolingService
{
	private sealed class CoolerState
	{
		private static readonly object AutomaticPowerState = new();

		private readonly SensorService _sensorService;
		private ICooler? _cooler;
		private ChannelWriter<CoolerChange>? _changeWriter;
		private readonly AsyncLock _lock;
		private object? _activeState;
		private StrongBox<byte>? _manualPowerState;
		private CoolerInformation _information;

		public SensorService SensorService => _sensorService;

		public CoolerState(SensorService sensorService, CoolerInformation information, CoolerConfiguration? persistedCoolerConfiguration)
		{
			_sensorService = sensorService;
			_lock = new();
			_information = information;
			if (persistedCoolerConfiguration is { } configuration)
			{
				_activeState = DeserializeCoolingMode(configuration.CoolingMode);
			}
		}

		private object? DeserializeCoolingMode(CoolingModeConfiguration coolingMode)
		{
			switch (coolingMode)
			{
			case AutomaticCoolingModeConfiguration automaticCoolingMode:
				return AutomaticPowerState;
			case FixedCoolingModeConfiguration fixedCoolingMode:
				return _manualPowerState = new(fixedCoolingMode.Power);
			case SoftwareCurveCoolingModeConfiguration softwareCurveCoolingMode:
				return DeserializeCoolingMode(softwareCurveCoolingMode);
			case HardwareCurveCoolingModeConfiguration hardwareCurveCoolingMode:
				return DeserializeCoolingMode(hardwareCurveCoolingMode);
			default:
				return null;
			}
		}

		private SoftwareCurveCoolerState DeserializeCoolingMode(SoftwareCurveCoolingModeConfiguration coolingMode)
		{
			switch (coolingMode.Curve)
			{
			case CoolingControlCurveConfiguration<sbyte> curveSByte: return DeserializeCoolingMode(coolingMode, curveSByte);
			case CoolingControlCurveConfiguration<byte> curveByte: return DeserializeCoolingMode(coolingMode, curveByte);
			case CoolingControlCurveConfiguration<short> curveInt16: return DeserializeCoolingMode(coolingMode, curveInt16);
			case CoolingControlCurveConfiguration<ushort> curveUInt16: return DeserializeCoolingMode(coolingMode, curveUInt16);
			case CoolingControlCurveConfiguration<int> curveInt32: return DeserializeCoolingMode(coolingMode, curveInt32);
			case CoolingControlCurveConfiguration<uint> curveUInt32: return DeserializeCoolingMode(coolingMode, curveUInt32);
			case CoolingControlCurveConfiguration<long> curveInt64: return DeserializeCoolingMode(coolingMode, curveInt64);
			case CoolingControlCurveConfiguration<ulong> curveUInt64: return DeserializeCoolingMode(coolingMode, curveUInt64);
			case CoolingControlCurveConfiguration<Half> curveFloat16: return DeserializeCoolingMode(coolingMode, curveFloat16);
			case CoolingControlCurveConfiguration<float> curveFloat32: return DeserializeCoolingMode(coolingMode, curveFloat32);
			case CoolingControlCurveConfiguration<double> curveFloat64: return DeserializeCoolingMode(coolingMode, curveFloat64);
			default: throw new InvalidOperationException();
			}
		}

		private SoftwareCurveCoolerState<TInput> DeserializeCoolingMode<TInput>(SoftwareCurveCoolingModeConfiguration coolingMode, CoolingControlCurveConfiguration<TInput> curve)
			where TInput : struct, INumber<TInput>
		{
			return new SoftwareCurveCoolerState<TInput>(this, coolingMode.SensorDeviceId, coolingMode.SensorId, coolingMode.DefaultPower, DeserializeCurve(curve));
		}

		private HardwareCurveCoolerState DeserializeCoolingMode(HardwareCurveCoolingModeConfiguration coolingMode)
		{
			switch (coolingMode.Curve)
			{
			case CoolingControlCurveConfiguration<sbyte> curveSByte: return DeserializeCoolingMode(coolingMode, curveSByte);
			case CoolingControlCurveConfiguration<byte> curveByte: return DeserializeCoolingMode(coolingMode, curveByte);
			case CoolingControlCurveConfiguration<short> curveInt16: return DeserializeCoolingMode(coolingMode, curveInt16);
			case CoolingControlCurveConfiguration<ushort> curveUInt16: return DeserializeCoolingMode(coolingMode, curveUInt16);
			case CoolingControlCurveConfiguration<int> curveInt32: return DeserializeCoolingMode(coolingMode, curveInt32);
			case CoolingControlCurveConfiguration<uint> curveUInt32: return DeserializeCoolingMode(coolingMode, curveUInt32);
			case CoolingControlCurveConfiguration<long> curveInt64: return DeserializeCoolingMode(coolingMode, curveInt64);
			case CoolingControlCurveConfiguration<ulong> curveUInt64: return DeserializeCoolingMode(coolingMode, curveUInt64);
			case CoolingControlCurveConfiguration<Half> curveFloat16: return DeserializeCoolingMode(coolingMode, curveFloat16);
			case CoolingControlCurveConfiguration<float> curveFloat32: return DeserializeCoolingMode(coolingMode, curveFloat32);
			case CoolingControlCurveConfiguration<double> curveFloat64: return DeserializeCoolingMode(coolingMode, curveFloat64);
			default: throw new InvalidOperationException();
			}
		}

		private HardwareCurveCoolerState<TInput> DeserializeCoolingMode<TInput>(HardwareCurveCoolingModeConfiguration coolingMode, CoolingControlCurveConfiguration<TInput> curve)
			where TInput : struct, INumber<TInput>
		{
			return new HardwareCurveCoolerState<TInput>(coolingMode.SensorId, DeserializeCurve(curve));
		}

		private static InterpolatedSegmentControlCurve<TInput, byte> DeserializeCurve<TInput>(CoolingControlCurveConfiguration<TInput> curve)
			where TInput : struct, INumber<TInput>
			=> new(curve.Points, MonotonicityValidators<byte>.IncreasingUpTo100);

		public CoolerInformation Information => _information;

		public async Task SetOnlineAsync(ICooler cooler, CoolerInformation information, ChannelWriter<CoolerChange> changeWriter, CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(default).ConfigureAwait(false))
			{
				_cooler = cooler;
				_information = information;
				_changeWriter = changeWriter;
				if (cooler is IManualCooler manualCooler)
				{
					if (!manualCooler.TryGetPower(out byte power))
					{
						power = manualCooler.MinimumPower;
					}
					if (_manualPowerState is null)
					{
						_manualPowerState = new(power);
					}
				}
				else
				{
					_manualPowerState = null;
				}
			}
		}

		public async Task SetOfflineAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(default).ConfigureAwait(false))
			{
				_cooler = null;
				_changeWriter = null;
				if (_activeState is SoftwareCurveCoolerState dynamicState)
				{
					await dynamicState.StopAsync().ConfigureAwait(false);
				}
			}
		}

		public async ValueTask RestoreStateAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				var activeState = _activeState;
				if (activeState is SoftwareCurveCoolerState dynamicState)
				{
					dynamicState.Start();
				}
				else if (activeState is HardwareCurveCoolerState hardwareCurveState)
				{
					_changeWriter!.TryWrite(hardwareCurveState.CreateCoolerChange((IHardwareCurveCooler)_cooler!));
				}
				else if (_manualPowerState is not null && ReferenceEquals(activeState, _manualPowerState))
				{
					SendManualPowerUpdate(_manualPowerState.Value);
				}
			}
		}

		public CoolerConfiguration? CreatePersistedConfiguration()
		{
			var activeState = Volatile.Read(ref _activeState);
			CoolingModeConfiguration activeCoolingMode;
			if (_activeState is SoftwareCurveCoolerState dynamicState)
			{
				activeCoolingMode = dynamicState.GetPersistedConfiguration();
			}
			else if (activeState is HardwareCurveCoolerState hardwareCurveState)
			{
				activeCoolingMode = hardwareCurveState.GetPersistedConfiguration();
			}
			else if (_manualPowerState is not null && ReferenceEquals(_activeState, _manualPowerState))
			{
				activeCoolingMode = new FixedCoolingModeConfiguration() { Power = _manualPowerState.Value };
			}
			else if (ReferenceEquals(activeState, AutomaticPowerState))
			{
				activeCoolingMode = new AutomaticCoolingModeConfiguration();
			}
			else
			{
				return null;
			}
			return new() { CoolingMode = activeCoolingMode };
		}

		public async ValueTask SetAutomaticPowerAsync(CancellationToken cancellationToken)
		{
			if (_cooler is not IAutomaticCooler automaticCooler) throw new InvalidOperationException("Automatic cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (ReferenceEquals(_activeState, AutomaticPowerState)) return;
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				_changeWriter!.TryWrite(CoolerChange.CreateAutomatic(automaticCooler));
				_activeState = AutomaticPowerState;
			}
		}

		public async ValueTask SetManualPowerAsync(byte power, CancellationToken cancellationToken)
		{
			if (_manualPowerState is null) throw new InvalidOperationException("Manual cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_manualPowerState.Value != power) _manualPowerState.Value = power;
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				else if (!ReferenceEquals(_activeState, _manualPowerState)) _activeState = _manualPowerState;
				SendManualPowerUpdate(power);
			}
		}

		public async ValueTask SetDynamicPowerAsyncAsync<TInput>
		(
			Guid sensorDeviceId,
			Guid sensorId,
			byte fallbackValue,
			InterpolatedSegmentControlCurve<TInput, byte> controlCurve,
			CancellationToken cancellationToken
		)
			where TInput : struct, INumber<TInput>
		{
			if (_manualPowerState is null) throw new InvalidOperationException("Manual cooling is not supported by this cooler.");

			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				var dynamicCoolerState = new SoftwareCurveCoolerState<TInput>(this, sensorDeviceId, sensorId, fallbackValue, controlCurve);
				dynamicCoolerState.Start();
				_activeState = dynamicCoolerState;
			}
		}

		public async ValueTask SetHardwareCurveAsync<TInput>(Guid sensorId, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
			where TInput : struct, INumber<TInput>
		{
			if (_cooler is not IHardwareCurveCooler cooler) throw new InvalidOperationException("Hardware cooling curves are not supported by this cooler.");
			var sensor = FindSensor(cooler, sensorId);

			switch (SensorService.GetSensorDataType(sensor.ValueType))
			{
			case SensorDataType.UInt8:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<byte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<ushort>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<uint>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<ulong>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.UInt128:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<UInt128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt8:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<sbyte>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<short>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<int>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<long>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.SInt128:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<Int128>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float16:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<Half>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float32:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<float>(), cancellationToken).ConfigureAwait(false);
				break;
			case SensorDataType.Float64:
				await SetHardwareCurveAsync(sensor, controlCurve.CastInput<double>(), cancellationToken).ConfigureAwait(false);
				break;
			default:
				throw new InvalidOperationException("Unsupported sensor data type.");
			}
		}

		private async ValueTask SetHardwareCurveAsync<TInput>(IHardwareCurveCoolerSensorCurveControl sensor, InterpolatedSegmentControlCurve<TInput, byte> controlCurve, CancellationToken cancellationToken)
			where TInput : struct, INumber<TInput>
		{
			if (sensor is not IHardwareCurveCoolerSensorCurveControl<TInput> typedSensor) throw new InvalidOperationException("This sensor has an incompatible value type.");
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (_activeState is IAsyncDisposable disposable) await disposable.DisposeAsync();
				var hardwareCurveState = new HardwareCurveCoolerState<TInput>(sensor.SensorId, controlCurve);
				_activeState = hardwareCurveState;
				_changeWriter!.TryWrite(CoolerChange.CreateHardwareCurve(typedSensor, controlCurve));
			}
		}

		private static IHardwareCurveCoolerSensorCurveControl FindSensor(IHardwareCurveCooler cooler, Guid sensorId)
		{
			foreach (var sensor in cooler.AvailableSensors)
			{
				if (sensor.SensorId == sensorId) return sensor;
			}
			throw new InvalidOperationException("The specified sensor ID is not a valid hardware control curve input source.");
		}

		public void SendManualPowerUpdate(byte power) => _changeWriter!.TryWrite(CoolerChange.CreateManual(Unsafe.As<IManualCooler>(_cooler!), power));
	}
}
