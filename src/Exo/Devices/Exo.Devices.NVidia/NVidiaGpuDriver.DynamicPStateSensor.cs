using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class DynamicPStateSensor : GroupQueriedSensor, IPolledSensor<byte>
	{
		private static Guid GetGuidForPState(NvApi.Gpu.Client.UtilizationDomain domain)
			=> domain switch
			{
				NvApi.Gpu.Client.UtilizationDomain.Graphics => GraphicsUtilizationSensorId,
				NvApi.Gpu.Client.UtilizationDomain.FrameBuffer => FrameBufferUtilizationSensorId,
				NvApi.Gpu.Client.UtilizationDomain.Video => VideoUtilizationSensorId,
				NvApi.Gpu.Client.UtilizationDomain.Bus => BusUtilizationSensorId,
				_ => throw new InvalidOperationException("Unsupported utilization domain.")
			};

		private byte _currentValue;
		private readonly NvApi.Gpu.Client.UtilizationDomain _domain;

		public Guid SensorId => GetGuidForPState(_domain);

		public DynamicPStateSensor(NvApi.PhysicalGpu gpu, NvApi.Gpu.Client.UtilizationDomain domain, byte value)
			: base(gpu)
		{
			_domain = domain;
			_currentValue = value;
		}

		public byte? ScaleMinimumValue => 0;
		public byte? ScaleMaximumValue => 100;

		public SensorUnit Unit => SensorUnit.Percent;

		public ValueTask<byte> GetValueAsync(CancellationToken cancellationToken)
			=> ValueTask.FromResult(GroupedQueryMode == GroupedQueryMode.Enabled ? _currentValue : QueryValue());

		public bool TryGetLastValue(out byte lastValue)
		{
			lastValue = _currentValue;
			return true;
		}

		private byte QueryValue() => throw new NotSupportedException("Non-grouped queries are not supported for this sensor.");

		public void OnValueRead(byte value) => _currentValue = value;
	}
}
