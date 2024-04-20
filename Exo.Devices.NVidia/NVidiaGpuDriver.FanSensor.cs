using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class FanSensor : IPolledSensor<uint>
	{
		private readonly NvApi.PhysicalGpu _gpu;

		public Guid SensorId => FanSpeedSensorId;

		public FanSensor(NvApi.PhysicalGpu gpu)
		{
			_gpu = gpu;
		}

		public uint? ScaleMinimumValue => null;
		public uint? ScaleMaximumValue => null;
		public SensorUnit Unit => SensorUnit.RotationsPerMinute;

		public ValueTask<uint> GetValueAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult(_gpu.GetTachReading());
	}
}
