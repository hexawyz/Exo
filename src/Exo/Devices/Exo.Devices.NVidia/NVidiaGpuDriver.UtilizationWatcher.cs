namespace Exo.Devices.NVidia;

public partial class NVidiaGpuDriver
{
	private sealed class UtilizationWatcher : IAsyncDisposable
	{
		private readonly NvApi.PhysicalGpu _gpu;
		private readonly UtilizationSensor _graphicsSensor;
		private readonly UtilizationSensor _frameBufferSensor;
		private readonly UtilizationSensor _videoSensor;
		private int _referenceCount;
		private readonly Lock _lock;
		private TaskCompletionSource _enableSignal;
		private CancellationTokenSource? _disableCancellationTokenSource;
		private CancellationTokenSource? _disposeCancellationTokenSource;
		private readonly Task _runTask;

		public UtilizationSensor GraphicsSensor => _graphicsSensor;
		public UtilizationSensor FrameBufferSensor => _frameBufferSensor;
		public UtilizationSensor VideoSensor => _videoSensor;

		public UtilizationWatcher(NvApi.PhysicalGpu gpu)
		{
			_gpu = gpu;
			_graphicsSensor = new(this, GraphicsUtilizationSensorId);
			_frameBufferSensor = new(this, FrameBufferUtilizationSensorId);
			_videoSensor = new(this, VideoUtilizationSensorId);
			_lock = new();
			_enableSignal = new();
			_disposeCancellationTokenSource = new();
			_runTask = RunAsync(_disposeCancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposeCancellationTokenSource, null) is { } cts)
			{
				cts.Cancel();
				Volatile.Read(ref _enableSignal).TrySetResult();
				await _runTask.ConfigureAwait(false);
				cts.Dispose();
			}
		}

		public void Acquire()
		{
			lock (_lock)
			{
				if (_referenceCount++ == 0)
				{
					_enableSignal.TrySetResult();
				}
			}
		}

		// This function is called by a sensor state to cancel grouped querying for it.
		// NB: The sensor state *WILL* ensure that this method is never called twice in succession for a given sensor.
		public void Release()
		{
			lock (_lock)
			{
				if (--_referenceCount == 0)
				{
					if (Interlocked.Exchange(ref _disableCancellationTokenSource, null) is { } cts)
					{
						cts.Cancel();
						cts.Dispose();
					}
				}
			}
		}

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await _enableSignal.Task.ConfigureAwait(false);
					if (cancellationToken.IsCancellationRequested) return;
					var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					var queryCancellationToken = cts.Token;
					Volatile.Write(ref _disableCancellationTokenSource, cts);
					try
					{
						await WatchValuesAsync(queryCancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException) when (queryCancellationToken.IsCancellationRequested)
					{
					}
					if (cancellationToken.IsCancellationRequested) return;
					cts = Interlocked.Exchange(ref _disableCancellationTokenSource, null);
					cts?.Dispose();
					Volatile.Write(ref _enableSignal, new());
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				// TODO: Log
			}
		}

		private async ValueTask WatchValuesAsync(CancellationToken cancellationToken)
		{
			await foreach (var utilizationValue in _gpu.WatchUtilizationAsync(1_000, cancellationToken).ConfigureAwait(false))
			{
				var sensor = utilizationValue.Domain switch
				{
					NvApi.Gpu.Client.UtilizationDomain.Graphics => _graphicsSensor,
					NvApi.Gpu.Client.UtilizationDomain.FrameBuffer => _frameBufferSensor,
					NvApi.Gpu.Client.UtilizationDomain.Video => _videoSensor,
					_ => null,
				};
				sensor?.OnDataReceived(utilizationValue.DateTime, utilizationValue.PerTenThousandValue);
			}
		}
	}
}
