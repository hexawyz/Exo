using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Exo.Contracts.Ui.Settings;
using Exo.Ui;
using Grpc.Net.Client;
using ProtoBuf.Grpc.Client;

namespace Exo.Settings.Ui.Services;

// The goal of this class is to synchronize states between the multiple components to ensure that everything can be reset properly when the service disconnects.
// The idea is that each component implements the IConnectedState interface and registers here to allow all states to be reset before restarting them afterwards.
// TODO: The GRPC service stuff should probably be refactored to not be hardcoded, and instead allow management of an "unlimited" number of service channels.
internal sealed class SettingsServiceConnectionManager : ServiceConnectionManager
{
	private sealed class ConnectedState : IDisposable
	{
		private readonly SynchronizationContext? _synchronizationContext;
		private readonly SettingsServiceConnectionManager _connectionManager;
		private readonly IConnectedState _connectedState;
		private Task? _runTask;

		public ConnectedState(SynchronizationContext? synchronizationContext, SettingsServiceConnectionManager connectionManager, IConnectedState connectedState)
		{
			_synchronizationContext = synchronizationContext;
			_connectionManager = connectionManager;
			_connectedState = connectedState;
		}

		public void Dispose() => _connectionManager.UnregisterState(_connectedState);

		public Task StartAsync(CancellationToken cancellationToken)
		{
			if (_synchronizationContext is null)
			{
				try
				{
					StartCore(cancellationToken);
					return Task.CompletedTask;
				}
				catch (Exception ex)
				{
					return Task.FromException(ex);
				}
			}
			else
			{
				var tcs = new TaskCompletionSource();
				_synchronizationContext.Post
				(
					static state =>
					{
						var t = (Tuple<ConnectedState, TaskCompletionSource, CancellationToken>)state!;
						try
						{
							t.Item1.StartCore(t.Item3);
							t.Item2.TrySetResult();
						}
						catch (Exception ex)
						{
							t.Item2.TrySetException(ex);
						}
					},
					Tuple.Create(this, tcs, cancellationToken)
				);
				return tcs.Task;
			}
		}

		private void StartCore(CancellationToken cancellationToken)
		{
			if (_runTask is not null) return;

			_runTask = RunAsync(cancellationToken);
		}

		public Task ResetAsync()
		{
			if (_synchronizationContext is null)
			{
				ResetCore();
				return Task.CompletedTask;
			}
			else
			{
				var tcs = new TaskCompletionSource(this);
				_synchronizationContext.Post
				(
					static state =>
					{
						var t = (TaskCompletionSource)state!;
						var connectedState = (ConnectedState)t.Task.AsyncState!;
						try
						{
							connectedState.ResetCore();
							t.TrySetResult();
						}
						catch (Exception ex)
						{
							t.TrySetException(ex);
						}
					},
					tcs
				);
				return tcs.Task;
			}
		}

		private void ResetCore()
		{
			try
			{
				_connectedState.Reset();
			}
			finally
			{
				_runTask = null;
			}
		}

		public Task WaitAsync() => _runTask ?? Task.CompletedTask;

		private async Task RunAsync(CancellationToken cancellationToken)
		{
			try
			{
				await _connectedState.RunAsync(cancellationToken).ConfigureAwait(false);
			}
			catch
			{
			}
		}
	}

	private static readonly object ConnectionStatusDisconnected = ConnectionStatus.Disconnected;
	private static readonly object ConnectionStatusConnected = ConnectionStatus.Connected;
	private static readonly object ConnectionStatusVersionMismatch = ConnectionStatus.VersionMismatch;

	// "Allocation-free" boxing of connection status enum values.
	private static object Box(ConnectionStatus connectionStatus)
		=> connectionStatus switch
		{
			ConnectionStatus.Disconnected => ConnectionStatusDisconnected,
			ConnectionStatus.Connected => ConnectionStatusConnected,
			ConnectionStatus.VersionMismatch => ConnectionStatusVersionMismatch,
			_ => throw new InvalidOperationException(),
		};

	// NB: The proper implementation should be the usage of weak references and ConditionalWeakTable here.
	// If we end up needing to dynamically register components at some point, the implementation should be upgraded.
	private readonly Dictionary<IConnectedState, ConnectedState> _connectedStates;
	private CancellationToken _disconnectionToken;
	private ConnectionStatus _connectionStatus;

	private readonly SynchronizationContext? _synchronizationContext;
	private readonly Action<SettingsServiceConnectionManager, ConnectionStatus> _connectionStatusChangeHandler;

	private ConnectionStatus ConnectionStatus => (ConnectionStatus)Volatile.Read(ref Unsafe.As<ConnectionStatus, uint>(ref _connectionStatus));

	public SettingsServiceConnectionManager(string pipeName, int reconnectDelay, string? version, Action<SettingsServiceConnectionManager, ConnectionStatus> connectionStatusChangeHandler)
		: base(pipeName, reconnectDelay, version)
	{
		_connectedStates = new();
		_synchronizationContext = SynchronizationContext.Current;
		_connectionStatusChangeHandler = connectionStatusChangeHandler;
	}

	private void NotifyConnectionStatusChanged(ConnectionStatus connectionStatus)
	{
		if (_connectionStatusChangeHandler is not null)
		{
			if (_synchronizationContext is not null)
			{
				_synchronizationContext.Post(state => _connectionStatusChangeHandler(this, (ConnectionStatus)state!), Box(connectionStatus));
			}
			else
			{
				_connectionStatusChangeHandler.Invoke(this, connectionStatus);
			}
		}
	}

	protected override void OnVersionMismatch()
	{
		lock (_connectedStates)
		{
			_connectionStatus = ConnectionStatus.VersionMismatch;
		}

		NotifyConnectionStatusChanged(ConnectionStatus.VersionMismatch);
	}

	protected override async Task OnConnectedAsync(GrpcChannel channel, CancellationToken disconnectionToken)
	{
		Task startStatesTask;
		lock (_connectedStates)
		{
			_connectionStatus = ConnectionStatus.Connected;
			_disconnectionToken = disconnectionToken;
			startStatesTask = StartStatesAsync(disconnectionToken);
		}
		NotifyConnectionStatusChanged(ConnectionStatus.Connected);
		await startStatesTask.ConfigureAwait(false);
	}

	protected override async Task OnDisconnectedAsync()
	{
		Task waitStatesTask;
		lock (_connectedStates)
		{
			waitStatesTask = WaitStatesAsync();
		}
		try
		{
			await waitStatesTask.ConfigureAwait(false);
		}
		catch
		{
			// NB: We expect tasks to throw exceptions as a result of the cancellation here.
		}

		Task resetStatesTask;
		lock (_connectedStates)
		{
			_connectionStatus = ConnectionStatus.Disconnected;
			resetStatesTask = ResetStatesAsync();
		}
		NotifyConnectionStatusChanged(ConnectionStatus.Disconnected);
		await resetStatesTask.ConfigureAwait(false);
	}

	private static void Connect<T>(GrpcChannel channel, TaskCompletionSource<T> taskCompletionSource)
		where T : class
		=> taskCompletionSource.TrySetResult(channel.CreateGrpcService<T>());

	private static void Reset<T>(ref TaskCompletionSource<T> taskCompletionSource)
	{
		if (!taskCompletionSource.Task.IsCompleted)
			taskCompletionSource.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(SettingsServiceConnectionManager).FullName)));
		Volatile.Write(ref taskCompletionSource, new());
	}

	public async ValueTask<IDisposable> RegisterStateAsync(IConnectedState state)
	{
		Task? startTask = null;
		ConnectedState stateWrapper;
		lock (_connectedStates)
		{
			stateWrapper = new ConnectedState(SynchronizationContext.Current, this, state);

			_connectedStates.Add(state, stateWrapper);
			if (ConnectionStatus == ConnectionStatus.Connected)
			{
				startTask = stateWrapper.StartAsync(_disconnectionToken);
			}
		}
		if (startTask is not null)
		{
			try
			{
				await startTask.ConfigureAwait(false);
			}
			catch
			{
				lock (_connectedStates)
				{
					_connectedStates.Remove(state);
				}
				throw;
			}
		}

		return stateWrapper;
	}

	private void UnregisterState(IConnectedState state)
	{
		lock (_connectedStates)
		{
			_connectedStates.Remove(state);
		}
	}

	private Task StartStatesAsync(CancellationToken disconnectionToken)
	{
		var tasks = new Task[_connectedStates.Count];
		int i = 0;
		foreach (var state in _connectedStates.Values)
		{
			try
			{
				tasks[i] = state.StartAsync(disconnectionToken);
			}
			catch (Exception ex)
			{
				tasks[i] = Task.FromException(ex);
			}
			i++;
		}
		return Task.WhenAll(tasks);
	}

	private Task WaitStatesAsync()
	{
		var tasks = new Task[_connectedStates.Count];
		int i = 0;
		foreach (var state in _connectedStates.Values)
		{
			try
			{
				tasks[i] = state.WaitAsync();
			}
			catch (Exception ex)
			{
				tasks[i] = Task.FromException(ex);
			}
			i++;
		}
		return Task.WhenAll(tasks);
	}

	private Task ResetStatesAsync()
	{
		var tasks = new Task[_connectedStates.Count];
		int i = 0;
		foreach (var state in _connectedStates.Values)
		{
			try
			{
				tasks[i] = state.ResetAsync();
			}
			catch (Exception ex)
			{
				tasks[i] = Task.FromException(ex);
			}
			i++;
		}
		return Task.WhenAll(tasks);
	}
}
