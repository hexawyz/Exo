using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class ClockSensor : GroupQueriedSensor, IPolledSensor<uint>
	{
		private static Guid GetGuidForClock(NvApi.Gpu.PublicClock clock)
			=> clock switch
			{
				NvApi.Gpu.PublicClock.Graphics => GraphicsFrequencySensorId,
				NvApi.Gpu.PublicClock.Memory => MemoryFrequencySensorId,
				NvApi.Gpu.PublicClock.Processor => ProcessorFrequencySensorId,
				NvApi.Gpu.PublicClock.Video => VideoFrequencySensorId,
				_ => throw new InvalidOperationException("Unsupported clock.")
			};

		private uint _currentValue;
		private readonly byte _clock;

		public Guid SensorId => GetGuidForClock((NvApi.Gpu.PublicClock)_clock);

		public ClockSensor(NvApi.PhysicalGpu gpu, NvApi.GpuClockFrequency clock)
			: base(gpu)
		{
			_currentValue = (uint)clock.FrequencyInKiloHertz;
			_clock = (byte)clock.Clock;
		}

		public uint? ScaleMinimumValue => null;
		public uint? ScaleMaximumValue => null;

		public SensorUnit Unit => SensorUnit.KiloHertz;

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
