using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class FanCoolerSensor : GroupQueriedSensor, IPolledSensor<uint>
	{
		private static Guid GetGuidForFan(uint fanId)
			=> fanId switch
			{
				1 => Fan1SpeedSensorId,
				2 => Fan2SpeedSensorId,
				_ => throw new InvalidOperationException("Unsupported fan.")
			};

		private uint _currentValue;
		private readonly byte _fanId;

		public Guid SensorId => GetGuidForFan(_fanId);

		public FanCoolerSensor(NvApi.PhysicalGpu gpu, NvApi.GpuFanStatus fanStatus)
			: base(gpu)
		{
			_currentValue = fanStatus.SpeedInRotationsPerMinute;
			_fanId = (byte)fanStatus.FanId;
		}

		public uint? ScaleMinimumValue => null;
		public uint? ScaleMaximumValue => null;

		public SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public ValueTask<uint> GetValueAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult(GroupedQueryMode == GroupedQueryMode.Enabled ? _currentValue : QueryValue());

		public bool TryGetLastValue(out uint lastValue)
		{
			lastValue = _currentValue;
			return true;
		}

		private uint QueryValue() => throw new NotSupportedException("Non-grouped queries are not supported for this sensor.");

		public void OnValueRead(uint value) => _currentValue = value;
	}
}
