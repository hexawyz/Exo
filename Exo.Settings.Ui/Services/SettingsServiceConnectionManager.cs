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

	// NB: The proper implementation should be the usage of weak references and ConditionalWeakTable here.
	// If we end up needing to dynamically register components at some point, the implementation should be upgraded.
	private readonly Dictionary<IConnectedState, ConnectedState> _connectedStates;
	private TaskCompletionSource<IDeviceService> _deviceServiceTaskCompletionSource;
	private TaskCompletionSource<IMouseService> _mouseServiceTaskCompletionSource;
	private TaskCompletionSource<IMonitorService> _monitorServiceTaskCompletionSource;
	private TaskCompletionSource<ILightingService> _lightingServiceTaskCompletionSource;
	private TaskCompletionSource<ISensorService> _sensorServiceTaskCompletionSource;
	private TaskCompletionSource<IProgrammingService> _programmingServiceTaskCompletionSource;
	private CancellationToken _disconnectionToken;
	private bool _isConnected;

	public bool IsConnected => _isConnected;

	public SettingsServiceConnectionManager(string pipeName, int reconnectDelay, string? version)
		: base(pipeName, reconnectDelay, version)
	{
		_connectedStates = new();
		_deviceServiceTaskCompletionSource = new();
		_mouseServiceTaskCompletionSource = new();
		_monitorServiceTaskCompletionSource = new();
		_lightingServiceTaskCompletionSource = new();
		_sensorServiceTaskCompletionSource = new();
		_programmingServiceTaskCompletionSource = new();
	}

	public Task<IDeviceService> GetDeviceServiceAsync(CancellationToken cancellationToken)
		=> _deviceServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IMouseService> GetMouseServiceAsync(CancellationToken cancellationToken)
		=> _mouseServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IMonitorService> GetMonitorServiceAsync(CancellationToken cancellationToken)
		=> _monitorServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<ILightingService> GetLightingServiceAsync(CancellationToken cancellationToken)
		=> _lightingServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<ISensorService> GetSensorServiceAsync(CancellationToken cancellationToken)
		=> _sensorServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	public Task<IProgrammingService> GetProgrammingServiceAsync(CancellationToken cancellationToken)
		=> _programmingServiceTaskCompletionSource.Task.WaitAsync(cancellationToken);

	protected override async Task OnConnectedAsync(GrpcChannel channel, CancellationToken disconnectionToken)
	{
		Connect(channel, _deviceServiceTaskCompletionSource);
		Connect(channel, _mouseServiceTaskCompletionSource);
		Connect(channel, _monitorServiceTaskCompletionSource);
		Connect(channel, _lightingServiceTaskCompletionSource);
		Connect(channel, _sensorServiceTaskCompletionSource);
		Connect(channel, _programmingServiceTaskCompletionSource);

		Task startStatesTask;
		lock (_connectedStates)
		{
			_isConnected = true;
			_disconnectionToken = disconnectionToken;
			startStatesTask = StartStatesAsync(disconnectionToken);
		}
		await startStatesTask.ConfigureAwait(false);
	}

	protected override async Task OnDisconnectedAsync()
	{
		Reset(ref _deviceServiceTaskCompletionSource);
		Reset(ref _mouseServiceTaskCompletionSource);
		Reset(ref _monitorServiceTaskCompletionSource);
		Reset(ref _lightingServiceTaskCompletionSource);
		Reset(ref _sensorServiceTaskCompletionSource);
		Reset(ref _programmingServiceTaskCompletionSource);

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
			_isConnected = false;
			resetStatesTask = ResetStatesAsync();
		}
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
			if (_isConnected)
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
