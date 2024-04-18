using System.Threading.Channels;
using Exo.Sensors;

namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class UtilizationSensor : IStreamedSensor<float>
	{
		private ChannelWriter<SensorDataPoint<float>>? _listener;
		private readonly UtilizationWatcher _watcher;

		public Guid SensorId { get; }

		public UtilizationSensor(UtilizationWatcher watcher, Guid sensorId)
		{
			_watcher = watcher;
			SensorId = sensorId;
		}

		public float? ScaleMinimumValue => 0;
		public float? ScaleMaximumValue => 100;
		public SensorUnit Unit => SensorUnit.Percent;

		public async IAsyncEnumerable<SensorDataPoint<float>> EnumerateValuesAsync(CancellationToken cancellationToken)
		{
			var channel = Channel.CreateUnbounded<SensorDataPoint<float>>(SharedOptions.ChannelOptions);
			if (Interlocked.CompareExchange(ref _listener, channel, null) is not null) throw new InvalidOperationException("An enumeration is already running.");
			try
			{
				_watcher.Acquire();
				try
				{
					await foreach (var dataPoint in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
					{
						yield return dataPoint;
					}
				}
				finally
				{
					_watcher.Release();
				}
			}
			finally
			{
				Volatile.Write(ref _listener, null);
			}
		}

		public void OnDataReceived(DateTime dateTime, uint value) => Volatile.Read(ref _listener)?.TryWrite(new SensorDataPoint<float>(dateTime, value * 0.01f));
	}
}
