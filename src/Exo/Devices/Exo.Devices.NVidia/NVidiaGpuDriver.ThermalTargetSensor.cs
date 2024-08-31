using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class ThermalTargetSensor : GroupQueriedSensor, IPolledSensor<short>
	{
		private static Guid GetGuidForThermalTarget(NvApi.Gpu.ThermalTarget thermalTarget)
			=> thermalTarget switch
			{
				NvApi.Gpu.ThermalTarget.Gpu => GpuThermalSensorId,
				NvApi.Gpu.ThermalTarget.Memory => MemoryThermalSensorId,
				NvApi.Gpu.ThermalTarget.PowerSupply => PowerSupplyThermalSensorId,
				NvApi.Gpu.ThermalTarget.Board => BoardThermalSensorId,
				_ => throw new InvalidOperationException("Unsupported thermal target.")
			};

		private short _currentValue;
		private readonly short _minValue;
		private readonly short _maxValue;
		private readonly sbyte _thermalTarget;
		private readonly byte _sensorIndex;

		public Guid SensorId => GetGuidForThermalTarget((NvApi.Gpu.ThermalTarget)_thermalTarget);

		public ThermalTargetSensor(NvApi.PhysicalGpu gpu, NvApi.Gpu.ThermalSensor thermalSensor, byte sensorIndex)
			: base(gpu)
		{
			_currentValue = (short)thermalSensor.CurrentTemp;
			_minValue = (short)thermalSensor.DefaultMinTemp;
			_maxValue = (short)thermalSensor.DefaultMaxTemp;
			_thermalTarget = (sbyte)thermalSensor.Target;
			_sensorIndex = sensorIndex;
		}

		public short? ScaleMinimumValue => _minValue;
		public short? ScaleMaximumValue => _maxValue;

		public SensorUnit Unit => SensorUnit.Celsius;

		public ValueTask<short> GetValueAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult(GroupedQueryMode == GroupedQueryMode.Enabled ? _currentValue : QueryValue());

		public bool TryGetLastValue(out short lastValue)
		{
			lastValue = _currentValue;
			return true;
		}

		private short QueryValue()
		{
			var result = Gpu.GetThermalSettings(1);
			return (short)result.CurrentTemp;
		}

		public void OnValueRead(short value) => _currentValue = value;
	}
}
