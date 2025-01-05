using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Exo.Cooling;

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

		public CoolerState(SensorService sensorService, CoolerInformation information)
		{
			_sensorService = sensorService;
			_lock = new();
			_information = information;
		}

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
				if (_activeState is DynamicCoolerState dynamicState)
				{
					await dynamicState.StopAsync().ConfigureAwait(false);
				}
			}
		}

		public async ValueTask RestoreStateAsync(CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				_cooler = null;
				var activeState = _activeState;
				if (activeState is DynamicCoolerState dynamicState)
				{
					dynamicState.Reset();
					dynamicState.Start();
				}
				else if (activeState is HardwareCurveCoolerState hardwareCurveState)
				{
					_changeWriter!.TryWrite(hardwareCurveState.CreateCoolerChange());
				}
				else if (_manualPowerState is not null && ReferenceEquals(activeState, _manualPowerState))
				{
					SendManualPowerUpdate(_manualPowerState.Value);
				}
			}
		}

		public PersistedCoolerConfiguration? CreatePersistedConfiguration()
		{
			var activeState = Volatile.Read(ref _activeState);
			ActiveCoolingMode activeCoolingMode;
			if (_activeState is DynamicCoolerState dynamicState)
			{
				activeCoolingMode = dynamicState.GetPersistedConfiguration();
			}
			else if (activeState is HardwareCurveCoolerState hardwareCurveState)
			{
				activeCoolingMode = hardwareCurveState.GetPersistedConfiguration();
			}
			else if (_manualPowerState is not null && ReferenceEquals(_activeState, _manualPowerState))
			{
				activeCoolingMode = new FixedCoolingMode() { Power = _manualPowerState.Value };
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
				else if (!ReferenceEquals(_activeState, _manualPowerState)) _activeState = AutomaticPowerState;
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
				var dynamicCoolerState = new DynamicCoolerState<TInput>(this, sensorDeviceId, sensorId, fallbackValue, controlCurve);
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
				var hardwareCurveState = new HardwareCurveCoolerState<TInput>(typedSensor, controlCurve);
				_activeState = hardwareCurveState;
				_changeWriter!.TryWrite(hardwareCurveState.CreateCoolerChange());
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
