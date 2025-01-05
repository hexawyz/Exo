using System.Numerics;
using Exo.Cooling;
using Exo.Cooling.Configuration;

namespace Exo.Service;

internal partial class CoolingService
{
	private abstract class SoftwareCurveCoolerState
	{
		private readonly CoolerState _coolerState;
		private CancellationTokenSource? _cancellationTokenSource;
		private Task? _runTask;

		protected CoolerState CoolerState => _coolerState;

		protected SoftwareCurveCoolerState(CoolerState coolerState)
		{
			_coolerState = coolerState;
			_cancellationTokenSource = new();
		}

		internal void Start()
		{
			if (_cancellationTokenSource is null) throw new InvalidOperationException();
			if (_runTask is not null) throw new InvalidOperationException();
			_runTask = RunAsync(_cancellationTokenSource!.Token);
		}

		public async ValueTask StopAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
			cts.Cancel();
			if (_runTask is not null)
			{
				await _runTask.ConfigureAwait(false);
			}
			cts.Dispose();
		}

		/// <summary>Resets the state, after it has been stopped.</summary>
		/// <remarks>
		/// This operation is important to allow for restarting a dynamic state after a device came back online from a previous time.
		/// This is actually a niche use case as cooling devices would generally be always on. However, devices can go offline for many reasons, so we definitely want to have this working.
		/// </remarks>
		/// <returns></returns>
		internal void Reset()
		{
			if (Interlocked.CompareExchange(ref _cancellationTokenSource, new CancellationTokenSource(), null) is not null) throw new InvalidOperationException();
		}

		/// <summary>Runs this dynamic state until it is requested to stop.</summary>
		/// <remarks>This method can be called multiple times but never more than once at a time.</remarks>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected abstract Task RunAsync(CancellationToken cancellationToken);

		public abstract SoftwareCurveCoolingModeConfiguration GetPersistedConfiguration();
	}

	private sealed class SoftwareCurveCoolerState<TInput> : SoftwareCurveCoolerState
		where TInput : struct, INumber<TInput>
	{
		private readonly InterpolatedSegmentControlCurve<TInput, byte> _controlCurve;
		private readonly Guid _sensorDeviceId;
		private readonly Guid _sensorId;
		private readonly byte _fallbackValue;

		public SoftwareCurveCoolerState(CoolerState coolerState, Guid sensorDeviceId, Guid sensorId, byte fallbackValue, InterpolatedSegmentControlCurve<TInput, byte> controlCurve) : base(coolerState)
		{
			_sensorDeviceId = sensorDeviceId;
			_sensorId = sensorId;
			_fallbackValue = fallbackValue;
			_controlCurve = controlCurve;
		}

		protected override async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					// Always reset the power to the fallback value when the sensor value is unknown.
					// This can have downsides, but if well configured, it can also avoid catastrophic failures when the sensor has become unavailable for some reason.
					CoolerState.SendManualPowerUpdate(_fallbackValue);
					await CoolerState.SensorService.WaitForSensorAsync(_sensorDeviceId, _sensorId, cancellationToken).ConfigureAwait(false);
					try
					{
						await foreach (var dataPoint in CoolerState.SensorService.WatchValuesAsync<TInput>(_sensorDeviceId, _sensorId, cancellationToken).ConfigureAwait(false))
						{
							// NB: The state lock is not acquired here, as we are guaranteed that this dynamic state will be disposed before any other update can occur.
							CoolerState.SendManualPowerUpdate(_controlCurve[dataPoint.Value]);
						}
					}
					catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
					{
					}
					catch (DeviceDisconnectedException)
					{
						// TODO: Log (Information)
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				// Reset the power to the fallback value in case of error, as the sensor value is now technically unknown.
				CoolerState.SendManualPowerUpdate(_fallbackValue);
				// TODO: Log (Error)
			}
		}

		public override SoftwareCurveCoolingModeConfiguration GetPersistedConfiguration()
			=> new()
			{
				SensorDeviceId = _sensorDeviceId,
				SensorId = _sensorId,
				DefaultPower = _fallbackValue,
				Curve = CreatePersistedCurve(_controlCurve)
			};
	}
}

