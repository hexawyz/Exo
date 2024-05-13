using System.Threading.Channels;
using Exo.Configuration;
using Exo.Features.Cooling;

namespace Exo.Service;

internal partial class CoolingService
{
	private sealed class DeviceState
	{
		public AsyncLock Lock { get; }
		public IConfigurationContainer DeviceConfigurationContainer { get; }
		public IConfigurationContainer<Guid> CoolingConfigurationContainer { get; }
		public bool IsConnected { get; set; }
		public CoolingDeviceInformation Information { get; set; }
		public LiveDeviceState? LiveDeviceState { get; set; }
		public Dictionary<Guid, CoolerState>? CoolerStates { get; set; }

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> coolingConfigurationContainer,
			bool isConnected,
			CoolingDeviceInformation information,
			LiveDeviceState? liveDeviceState,
			Dictionary<Guid, CoolerState>? coolerStates
		)
		{
			Lock = new();
			DeviceConfigurationContainer = deviceConfigurationContainer;
			CoolingConfigurationContainer = coolingConfigurationContainer;
			IsConnected = isConnected;
			Information = information;
			LiveDeviceState = liveDeviceState;
			CoolerStates = coolerStates;
		}
	}

	private sealed class LiveDeviceState : IAsyncDisposable
	{
		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _runTask;

		public LiveDeviceState(ICoolingControllerFeature coolingControllerFeature, ChannelReader<CoolerState> changeReader)
		{
			_cancellationTokenSource = new();
			_runTask = RunAsync(coolingControllerFeature, changeReader, _cancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;
			cts.Cancel();
			await _runTask.ConfigureAwait(false);
			cts.Dispose();
		}

		private async Task RunAsync(ICoolingControllerFeature coolingControllerFeature, ChannelReader<CoolerState> changeReader, CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					await changeReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);

					try
					{
						while (changeReader.TryRead(out var changedCooler))
						{
						}

						await coolingControllerFeature.ApplyChangesAsync(cancellationToken).ConfigureAwait(false);
					}
					catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
					{
						// TODO: Log
					}
				}
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
			}
		}
	}
}
