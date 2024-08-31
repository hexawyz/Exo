using Exo.I2C;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class MonitorI2CBus : II2cBus
	{
		private readonly NvApi.PhysicalGpu _gpu;
		private readonly uint _outputId;

		public MonitorI2CBus(NvApi.PhysicalGpu gpu, uint outputId)
		{
			_gpu = gpu;
			_outputId = outputId;
		}

		public ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			_gpu.I2CMonitorWrite(_outputId, (byte)(address << 1), bytes);
			return ValueTask.CompletedTask;
		}

		public ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			_gpu.I2CMonitorWrite(_outputId, (byte)(address << 1), register, bytes);
			return ValueTask.CompletedTask;
		}

		public ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			_gpu.I2CMonitorRead(_outputId, (byte)(address << 1), bytes);
			return ValueTask.CompletedTask;
		}

		public ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			_gpu.I2CMonitorRead(_outputId, (byte)(address << 1), register, bytes);
			return ValueTask.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
