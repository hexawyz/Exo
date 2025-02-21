using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Exo.Cooling;
using Exo.Features.Cooling;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class FanCoolingControl : ICoolingControllerFeature
	{
		private readonly uint[] _fanIds;
		private readonly short[] _statuses;
		private readonly NvApi.PhysicalGpu _physicalGpu;
		private bool _hasChanged;
		private readonly FanCooler[] _coolers;

		public FanCoolingControl(NvApi.PhysicalGpu physicalGpu, ReadOnlySpan<NvApi.GpuFanInfo> fanInfos, ReadOnlySpan<NvApi.GpuFanStatus> fanStatuses, ReadOnlySpan<NvApi.GpuFanControl> fanControls)
		{
			var fanIds = new uint[fanControls.Length];
			var powers = new short[fanControls.Length];
			var coolers = new FanCooler[fanControls.Length];

			// Keep track of how many know coolers have been registered. We will hide the unknown IDs for safety. (Only 1 and 2 seen at the time of writing this)
			int registeredCoolerCount = 0;
			for (int i = 0; i < fanControls.Length; i++)
			{
				ref readonly var fanStatus = ref fanStatuses[i];
				ref readonly var fanControl = ref fanControls[i];

				fanIds[i] = fanControl.FanId;
				powers[i] = fanControl.CoolingMode == NvApi.FanCoolingMode.Manual ? (short)fanControl.Power : (short)256;

				Guid coolerId;
				Guid sensorId;
				if (fanControl.FanId is 1)
				{
					coolerId = Fan1CoolerId;
					sensorId = Fan1SpeedSensorId;
				}
				else if (fanControl.FanId is 2)
				{
					coolerId = Fan2CoolerId;
					sensorId = Fan2SpeedSensorId;
				}
				else
				{
					// Skip "unknown" fans
					// TODO: The fan IDs seem pretty reliable starting at 1 (probably to detect empty IDs), so we could probably hardcode a few more fan IDs.
					continue;
				}
				coolers[registeredCoolerCount++] = new(this, i, coolerId, sensorId, checked((byte)fanStatus.MinimumPower), checked((byte)fanStatus.MaximumPower));
			}

			if (registeredCoolerCount < coolers.Length)
			{
				coolers = coolers[..registeredCoolerCount];
			}

			_physicalGpu = physicalGpu;
			_fanIds = fanIds;
			_statuses = powers;
			_coolers = coolers;
		}

		ImmutableArray<ICooler> ICoolingControllerFeature.Coolers => ImmutableCollectionsMarshal.AsImmutableArray((ICooler[])_coolers);

		ValueTask ICoolingControllerFeature.ApplyChangesAsync(CancellationToken cancellationToken)
		{
			if (_hasChanged)
			{
				try
				{
					var fanIds = _fanIds;
					var statuses = _statuses;
					Span<NvApi.GpuFanControl> fanControls = stackalloc NvApi.GpuFanControl[fanIds.Length];

					int changedCount = 0;
					for (int i = 0; i < fanIds.Length; i++)
					{
						uint fanId = fanIds[i];
						ref short statusRef = ref statuses[i];
						short status = statusRef;
						if (status < 0)
						{
							status &= 0x7FFF;
							bool isManual = (status & 0x100) == 0;
							fanControls[changedCount++] = new(fanId, isManual ? (byte)status : (byte)0, isManual ? NvApi.FanCoolingMode.Manual : NvApi.FanCoolingMode.Automatic);
							statusRef = status;
						}
					}

					if (changedCount > 0)
					{
						_physicalGpu.SetFanCoolersControl(fanControls[..changedCount]);
					}

					_hasChanged = false;
				}
				catch (Exception ex)
				{
					return ValueTask.FromException(ex);
				}
			}
			return ValueTask.CompletedTask;
		}

		private sealed class FanCooler : ICooler, IAutomaticCooler, IManualCooler
		{
			private readonly FanCoolingControl _control;
			private readonly int _index;
			private readonly Guid _coolerId;
			private readonly Guid _sensorId;
			private readonly byte _minimumPower;
			private readonly byte _maximumPower;

			public FanCooler(FanCoolingControl control, int index, Guid coolerId, Guid sensorId, byte minimumPower, byte maximumPower)
			{
				_control = control;
				_index = index;
				_coolerId = coolerId;
				_sensorId = sensorId;
				_minimumPower = minimumPower;
				_maximumPower = maximumPower;
			}

			private sbyte Power
			{
				get
				{
					short status = _control._statuses[_index];
					return (status & 0x100) == 0 ? (sbyte)status : (sbyte)-1;
				}
				set
				{
					ref short statusRef = ref _control._statuses[_index];
					short status = statusRef;
					sbyte power = (status & 0x100) == 0 ? (sbyte)status : (sbyte)-1;
					if (value != power)
					{
						status = (short)(status & 0x7E00 | (value >= 0 ? 0x8000 | (byte)value : 0x8100));
						Volatile.Write(ref statusRef, status);
						_control._hasChanged = true;
					}
				}
			}

			Guid ICooler.CoolerId => _coolerId;
			Guid? ICooler.SpeedSensorId => _sensorId;
			CoolerType ICooler.Type => CoolerType.Fan;
			CoolingMode ICooler.CoolingMode => Power < 0 ? CoolingMode.Automatic : CoolingMode.Manual;

			void IAutomaticCooler.SwitchToAutomaticCooling() => Power = -1;

			void IManualCooler.SetPower(byte power)
			{
				ArgumentOutOfRangeException.ThrowIfLessThan(power, _minimumPower);
				ArgumentOutOfRangeException.ThrowIfGreaterThan(power, _maximumPower);
				Power = (sbyte)power;
			}

			bool IManualCooler.TryGetPower(out byte power)
			{
				var p = Power;
				if ((byte)p <= 100)
				{
					power = (byte)p;
					return true;
				}
				else
				{
					power = 0;
					return false;
				}
			}

			// NB: I didn't expose maximum power in the API, as it would seem stupid for it not to be 100.
			// TODO: Let's maybe add a warning log if the max power of a fan is not 100 for some reason.
			byte IConfigurableCooler.MinimumPower => _minimumPower;
			bool IConfigurableCooler.CanSwitchOff => false;
		}
	}
}
