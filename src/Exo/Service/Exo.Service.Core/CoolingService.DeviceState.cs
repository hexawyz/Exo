using System.Collections.Immutable;
using System.Runtime.InteropServices;
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
		public bool IsConnected { get; private set; }
		public LiveDeviceState? LiveDeviceState { get; private set; }
		public Dictionary<Guid, CoolerState> Coolers { get; }
		public Guid DeviceId { get; }

		public DeviceState
		(
			IConfigurationContainer deviceConfigurationContainer,
			IConfigurationContainer<Guid> coolingConfigurationContainer,
			Guid deviceId,
			Dictionary<Guid, CoolerState> coolers
		)
		{
			Lock = new();
			DeviceConfigurationContainer = deviceConfigurationContainer;
			CoolingConfigurationContainer = coolingConfigurationContainer;
			DeviceId = deviceId;
			Coolers = coolers;
		}

		public async ValueTask SetOnlineAsync(LiveDeviceState? liveDeviceState, CancellationToken cancellationToken)
		{
			using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				LiveDeviceState = liveDeviceState;
				IsConnected = true;
				foreach (var cooler in Coolers.Values)
				{
					await cooler.RestoreStateAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		public async ValueTask SetOfflineAsync(CancellationToken cancellationToken)
		{
			using (await Lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				IsConnected = false;
				if (LiveDeviceState is { } liveDeviceState)
				{
					await liveDeviceState.DisposeAsync().ConfigureAwait(false);
				}
				foreach (var coolerState in Coolers.Values)
				{
					await coolerState.SetOfflineAsync(cancellationToken).ConfigureAwait(false);
				}
			}
		}

		public CoolingDeviceInformation CreateInformation()
		{
			var infos = new CoolerInformation[Coolers.Count];
			int i = 0;
			foreach (var coolerState in Coolers.Values)
			{
				infos[i++] = coolerState.Information;
			}
			return new CoolingDeviceInformation(DeviceId, ImmutableCollectionsMarshal.AsImmutableArray(infos));
		}
	}

	private sealed class LiveDeviceState : IAsyncDisposable
	{
		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _runTask;

		public LiveDeviceState(ICoolingControllerFeature coolingControllerFeature, ChannelReader<CoolerChange> changeReader)
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

		private async Task RunAsync(ICoolingControllerFeature coolingControllerFeature, ChannelReader<CoolerChange> changeReader, CancellationToken cancellationToken)
		{
			try
			{
				while (true)
				{
					// NB: This is somewhat imperfect, but we rely on the thread activation delay to allow for processing more than one update in the same batch if necessary.
					// The idea is that with the time it takes to activate the thread, plus dequeue and execute the (likely quick) code for the update, an associated update
					// could have been published and be ready for processing immediately afterwards.
					// We can't/don't want to introduce an artificial delay other than this for the processing of cooling updates, but that should still help with batching.
					// Worst case, updates are not batched and we can live with it. Operations will still be serialized in the end, which is what is most important.
					await changeReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);

					try
					{
						while (changeReader.TryRead(out var change))
						{
							try
							{
								change.Execute();
							}
							catch (Exception ex)
							{
								// TODO: Log
							}
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
