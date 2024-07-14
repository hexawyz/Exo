using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using Exo.Contracts.Ui.Overlay;
using Exo.Features.Monitors;
using Exo.I2C;
using ProtoBuf.Grpc;

namespace Exo.Service.Grpc;

/// <summary>This is the connector for the monitor control proxy.</summary>
/// <remarks>
/// This is certainly the most complex external-facing service, as it does some heavy marshalling logic for a service provided from within the UI helper app.
/// Exposing this here does not impose a heavy risk on the service, as in the worst case, a malicious client would intercept monitor control requests and/or break proper function of the monitor feature.
/// </remarks>
internal class GrpcMonitorControlProxyService : IMonitorControlProxyService, IMonitorControlService
{
	private static readonly BoundedChannelOptions BoundedChannelOptions = new(10) { AllowSynchronousContinuations = true, FullMode = BoundedChannelFullMode.Wait, SingleWriter = true, SingleReader = true };
	private static readonly UnboundedChannelOptions UnboundedChannelOptions = new() { AllowSynchronousContinuations = true, SingleWriter = true, SingleReader = true };

	/// <summary>This represents one instance of a connected service.</summary>
	/// <remarks>
	/// <para>
	/// While we do not want it to happen, it is possible for multiple clients to be connected at the same time, and we need to keep an independent state for each of them.
	/// We consider all the clients to be equivalent to each other, but each have their own independent sate, so we need to be clear regarding to when such a client becomes available or unavailable.
	/// </para>
	/// <para>
	/// This class does also support the much more normal and expected case of having a single client at a time, but which is sometimes disconnected then reconnected.
	/// Because we have the knowledge and are able to handle individual client instances in a structured way, this will make state management on the consumer side of this service easier.
	/// </para>
	/// </remarks>
	private sealed class Session
	{
		private readonly GrpcMonitorControlProxyService _service;
		private readonly Guid _sessionId;

		// There are 6 services to initialize for the state to be ready.
		private const int NumberOfServicesToInitialize = 6;
		private ServiceState<AdapterRequest, AdapterResponse>? _adapterService;
		private ServiceState<MonitorRequest, MonitorResponse>? _monitorService;
		private ServiceState<MonitorCapabilitiesRequest, MonitorCapabilitiesResponse>? _capabilitiesService;
		private ServiceState<MonitorVcpGetRequest, MonitorVcpGetResponse>? _monitorVcpGetService;
		private ServiceState<MonitorVcpSetRequest, MonitorVcpSetResponse>? _monitorVcpSetService;
		private ChannelWriter<MonitorReleaseRequest>? _monitorReleaseWriter;

		private readonly object _lock;
		private TaskCompletionSource? _readySignal;
		private Session? _nextSession;
		private int _initializedServiceCount;

		public Session(GrpcMonitorControlProxyService service, Guid sessionId)
		{
			_service = service;
			_sessionId = sessionId;
			_lock = new();
			_readySignal = new();
		}

		public ServiceState<AdapterRequest, AdapterResponse> AdapterService => _adapterService ?? throw new InvalidOperationException("Service disconnected.");
		public ServiceState<MonitorRequest, MonitorResponse> MonitorService => _monitorService ?? throw new InvalidOperationException("Service disconnected.");
		public ServiceState<MonitorCapabilitiesRequest, MonitorCapabilitiesResponse> CapabilitiesService => _capabilitiesService ?? throw new InvalidOperationException("Service disconnected.");
		public ServiceState<MonitorVcpGetRequest, MonitorVcpGetResponse> MonitorVcpGetService => _monitorVcpGetService ?? throw new InvalidOperationException("Service disconnected.");
		public ServiceState<MonitorVcpSetRequest, MonitorVcpSetResponse> MonitorVcpSetService => _monitorVcpSetService ?? throw new InvalidOperationException("Service disconnected.");
		public ChannelWriter<MonitorReleaseRequest> MonitorReleaseWriter => _monitorReleaseWriter ?? throw new InvalidOperationException("Service disconnected.");

		// NB: MUST be called within the main lock (of the GRPC service).
		public Session? NextSession => _nextSession;

		// NB: MUST be called within the main lock (of the GRPC service).
		public void EnqueueSession(Session session)
		{
			var currentSession = this;
			while (currentSession._nextSession is { } nextSession)
			{
				currentSession = nextSession;
			}
			currentSession._nextSession = session;
		}

		// NB: MUST be called within the main lock (of the GRPC service).
		public void DequeueSession(Session session)
		{
			var currentSession = this;
			while (currentSession._nextSession is { } nextSession)
			{
				if (!ReferenceEquals(nextSession, session))
				{
					currentSession = nextSession;
				}
				else
				{
					currentSession._nextSession = session._nextSession;
					break;
				}
			}
		}

		public Task WaitAsync(CancellationToken cancellationToken)
			=> _readySignal is { } tcs
				? _readySignal.Task.WaitAsync(cancellationToken)
				: Task.FromException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(Session).FullName)));

		private ref ServiceState<TRequest, TResponse>? GetServiceReference<TRequest, TResponse>()
			where TRequest : IRequestId
			where TResponse : IRequestId
		{
			if (typeof(TRequest) == typeof(AdapterRequest) && typeof(TResponse) == typeof(AdapterResponse))
				return ref Unsafe.As<ServiceState<AdapterRequest, AdapterResponse>?, ServiceState<TRequest, TResponse>?>(ref _adapterService);

			if (typeof(TRequest) == typeof(MonitorRequest) && typeof(TResponse) == typeof(MonitorResponse))
				return ref Unsafe.As<ServiceState<MonitorRequest, MonitorResponse>?, ServiceState<TRequest, TResponse>?>(ref _monitorService);

			if (typeof(TRequest) == typeof(MonitorCapabilitiesRequest) && typeof(TResponse) == typeof(MonitorCapabilitiesResponse))
				return ref Unsafe.As<ServiceState<MonitorCapabilitiesRequest, MonitorCapabilitiesResponse>?, ServiceState<TRequest, TResponse>?>(ref _capabilitiesService);

			if (typeof(TRequest) == typeof(MonitorVcpGetRequest) && typeof(TResponse) == typeof(MonitorVcpGetResponse))
				return ref Unsafe.As<ServiceState<MonitorVcpGetRequest, MonitorVcpGetResponse>?, ServiceState<TRequest, TResponse>?>(ref _monitorVcpGetService);

			if (typeof(TRequest) == typeof(MonitorVcpSetRequest) && typeof(TResponse) == typeof(MonitorVcpSetResponse))
				return ref Unsafe.As<ServiceState<MonitorVcpSetRequest, MonitorVcpSetResponse>?, ServiceState<TRequest, TResponse>?>(ref _monitorVcpSetService);

			// NB: As you guessed by reading this code, all possible request/response combinations are hardcoded in this helper method in order to support true generic code in other parts.
			throw new InvalidOperationException("Unsupported request and response types.");
		}

		// NB: MUST be called within both locks.
		private void IncrementReferenceCount()
		{
			if (_initializedServiceCount == NumberOfServicesToInitialize)
			{
				if (_readySignal?.TrySetResult() == true)
				{
					_service.RegisterActiveSession(this);
				}
			}
		}

		// NB: MUST be called within both locks.
		private void DecrementReferenceCount()
		{
			if (--_initializedServiceCount == 0)
			{
				_service.UnregisterSession(this);
				_nextSession = null;
				_initializedServiceCount = int.MinValue >> 1;
				if (_readySignal is not null && !_readySignal.Task.IsCompleted)
				{
					_readySignal.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(Session).FullName)));
				}
				else
				{
					_readySignal = null;
				}
			}
		}

		// NB: Must be called within the main lock (of the GRPC service).
		public ServiceState<TRequest, TResponse> InitializeService<TRequest, TResponse>(IAsyncEnumerable<TResponse> responses)
			where TRequest : IRequestId
			where TResponse : IRequestId
		{
			ref var storage = ref GetServiceReference<TRequest, TResponse>();
			ServiceState<TRequest, TResponse> service;
			lock (_lock)
			{
				if (storage is not null) throw new InvalidOperationException("The processing endpoint is already connected.");
				if (_initializedServiceCount < 0) throw new ObjectDisposedException(typeof(Session).FullName);
				storage = service = new ServiceState<TRequest, TResponse>(this, responses);
				IncrementReferenceCount();
			}
			return service;
		}

		// NB: This is called outside of the main lock (of the GRPC service), so this method acquires it.
		internal void OnServiceDisposed<TRequest, TResponse>(ServiceState<TRequest, TResponse> service)
			where TRequest : IRequestId
			where TResponse : IRequestId
		{
			ref var storage = ref GetServiceReference<TRequest, TResponse>();
			lock (_service._lock)
			{
				lock (_lock)
				{
					var oldValue = storage;
					if (oldValue != service) throw new InvalidOperationException("The current service reference does not match the parameter.");
					storage = null;
					if (--_initializedServiceCount == 0)
					{
						_service._sessions.Remove(_sessionId, out _);
						_initializedServiceCount = int.MinValue >> 1;
						if (_readySignal is not null && !_readySignal.Task.IsCompleted)
						{
							_readySignal.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(Session).FullName)));
						}
						else
						{
							_readySignal = null;
						}
					}
				}
			}
		}

		public void SetMonitorReleaseWriter(ChannelWriter<MonitorReleaseRequest> writer)
		{
			lock (_lock)
			{
				if (_monitorReleaseWriter is not null) throw new InvalidOperationException("The processing endpoint is already connected.");
				if (_initializedServiceCount < 0) throw new ObjectDisposedException(typeof(Session).FullName);
				_monitorReleaseWriter = writer;
				IncrementReferenceCount();
			}
		}

		public void ClearMonitorReleaseWriter()
		{
			lock (_service._lock)
			{
				lock (_lock)
				{
					var oldValue = _monitorReleaseWriter;
					_monitorReleaseWriter = null;
					if (--_initializedServiceCount == 0)
					{
						_service._sessions.Remove(_sessionId, out _);
						_initializedServiceCount = int.MinValue >> 1;
						if (_readySignal is not null && !_readySignal.Task.IsCompleted)
						{
							_readySignal.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(Session).FullName)));
						}
						else
						{
							_readySignal = null;
						}
					}
				}
			}
		}
	}

	private sealed class ServiceState<TRequest, TResponse> : IAsyncDisposable
		where TRequest : IRequestId
		where TResponse : IRequestId
	{
		private readonly Session _session;
		private readonly Channel<TRequest> _requests;
		private int _requestId;
		private readonly Dictionary<int, TaskCompletionSource<TResponse>> _pendingRequests;

		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _runTask;

		public ServiceState(Session session, IAsyncEnumerable<TResponse> responses)
		{
			_session = session;
			_requests = Channel.CreateBounded<TRequest>(BoundedChannelOptions);
			_pendingRequests = new();
			_cancellationTokenSource = new();
			_runTask = RunAsync(responses, _cancellationTokenSource.Token);
		}

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

			cts.Cancel();
			await _runTask.ConfigureAwait(false);
			_session.OnServiceDisposed(this);
			cts.Dispose();
		}

		private async Task RunAsync(IAsyncEnumerable<TResponse> responses, CancellationToken cancellationToken)
		{
			try
			{
				await _session.WaitAsync(cancellationToken).ConfigureAwait(false);
				await foreach (var response in responses.ConfigureAwait(false))
				{
					lock (_pendingRequests)
					{
						if (_pendingRequests.Remove(response.RequestId, out var state))
						{
							state.TrySetResult(response);
						}
					}
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

		public int GetRequestId() => Interlocked.Increment(ref _requestId);

		public async ValueTask<TResponse> SendRequestAsync(TRequest request, CancellationToken cancellationToken)
		{
			TaskCompletionSource<TResponse> tcs;
			int requestId = request.RequestId;
			lock (_pendingRequests)
			{
				tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
				_pendingRequests.Add(requestId, tcs);
				_requests.Writer.WriteAsync(request);
			}
			try
			{
				return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
			{
				lock (_pendingRequests)
				{
					_pendingRequests.Remove(requestId);
				}
				tcs.TrySetCanceled(ex.CancellationToken);
				throw;
			}
		}

		public IAsyncEnumerable<TRequest> EnumerateAllRequestsAsync(CancellationToken cancellationToken) => _requests.Reader.ReadAllAsync(cancellationToken);
	}

	private sealed class Adapter : IMonitorControlAdapter
	{
		private readonly Session _session;
		private readonly ulong _adapterId;

		public Adapter(Session session, ulong adapterId)
		{
			_session = session;
			_adapterId = adapterId;
		}

		async Task<IMonitorControlMonitor> IMonitorControlAdapter.ResolveMonitorAsync(ushort vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		{
			var monitorService = _session.MonitorService;

			var response = await monitorService.SendRequestAsync
			(
				new()
				{
					RequestId = monitorService.GetRequestId(),
					AdapterId = _adapterId,
					EdidVendorId = vendorId,
					EdidProductId = productId,
					IdSerialNumber = idSerialNumber,
					SerialNumber = serialNumber,
				},
				cancellationToken
			).ConfigureAwait(false);
			ValidateResponseStatus(response.Status);
			return new Monitor(_session, response.MonitorHandle);
		}
	}

	private sealed class Monitor : IMonitorControlMonitor
	{
		private readonly Session _session;
		private readonly uint _monitorHandle;
		private uint _isDisposed;

		public Monitor(Session session, uint monitorHandle)
		{
			_session = session;
			_monitorHandle = monitorHandle;
		}

		void IDisposable.Dispose()
		{
			if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
			{
				_session.MonitorReleaseWriter.TryWrite(new MonitorReleaseRequest() { MonitorHandle = _monitorHandle });
			}
		}

		private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, typeof(Monitor));

		async Task<ImmutableArray<byte>> IMonitorControlMonitor.GetCapabilitiesAsync(CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var capabilitiesService = _session.CapabilitiesService;
			var response = await capabilitiesService.SendRequestAsync(new() { RequestId = capabilitiesService.GetRequestId(), MonitorHandle = _monitorHandle }, cancellationToken).ConfigureAwait(false);
			ValidateResponseStatus(response.Status);
			return response.Utf8Capabilities;
		}

		async Task<VcpFeatureResponse> IMonitorControlMonitor.GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var monitorVcpGetService = _session.MonitorVcpGetService;
			var response = await monitorVcpGetService.SendRequestAsync(new() { RequestId = monitorVcpGetService.GetRequestId(), MonitorHandle = _monitorHandle, VcpCode = vcpCode }, cancellationToken).ConfigureAwait(false);
			ValidateResponseStatus(response.Status);
			return new(response.CurrentValue, response.MaximumValue, response.IsTemporary);
		}

		async Task IMonitorControlMonitor.SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var monitorVcpSetService = _session.MonitorVcpSetService;
			var response = await monitorVcpSetService.SendRequestAsync(new() { RequestId = monitorVcpSetService.GetRequestId(), MonitorHandle = _monitorHandle, VcpCode = vcpCode, Value = value }, cancellationToken).ConfigureAwait(false);
			ValidateResponseStatus(response.Status);
		}
	}

	private static void ValidateResponseStatus(ResponseStatus status)
	{
		switch (status)
		{
		case ResponseStatus.Success: return;
		case ResponseStatus.NotFound: throw new Exception("Not found.");
		case ResponseStatus.Error: throw new Exception("Error.");
		default: throw new InvalidOperationException("Invalid status.");
		}
	}

	private readonly object _lock = new();
	private readonly Dictionary<Guid, Session> _sessions = new();
	private object? _currentSessionOrTaskCompletionSource;

	// NB: MUST be called within the lock.
	private void RegisterActiveSession(Session session)
	{
		if (_currentSessionOrTaskCompletionSource is not null)
		{
			if (_currentSessionOrTaskCompletionSource is not TaskCompletionSource<Session> tcs)
			{
				Unsafe.As<Session>(_currentSessionOrTaskCompletionSource).EnqueueSession(session);
				return;
			}
			else
			{
				tcs.TrySetResult(session);
			}
		}
		_currentSessionOrTaskCompletionSource = session;
	}

	// NB: MUST be called within the lock.
	private void UnregisterSession(Session session)
	{
		// The most common case we should encounter (ideally 100% of the time), is that the current session is the one we want to unregister.
		if (ReferenceEquals(_currentSessionOrTaskCompletionSource, session))
		{
			// NB: The NextSession value should be cleared after the call to this method, in order to reduce the chances to leak session objects.
			// This clearing will be done by the caller, as this method is called in Session.DecrementReferenceCount.
			_currentSessionOrTaskCompletionSource = session.NextSession;
		}
		else if (_currentSessionOrTaskCompletionSource is Session otherSession)
		{
			// NB: the value should always be a value of type Session, but the type-check above is done as a sanity check.
			// If we want to register a session that is not the current (active) one, we defer the dequeuing to the session object.
			otherSession.DequeueSession(session);
		}
	}

	private ValueTask<Session> GetSessionAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			// We can be in three situations here:
			// - Uninitialized: Need to wait for a session to become available (which could never happen)
			// - Already waiting for a session: There will be one more waiter
			// - At least one active session is ready: No need to wait.
			if (_currentSessionOrTaskCompletionSource is null)
			{
				var tcs = new TaskCompletionSource<Session>(TaskCreationOptions.RunContinuationsAsynchronously);
				_currentSessionOrTaskCompletionSource = tcs;
				return new(tcs.Task.WaitAsync(cancellationToken));
			}
			else if (_currentSessionOrTaskCompletionSource is TaskCompletionSource<Session> tcs2)
			{
				return new(tcs2.Task.WaitAsync(cancellationToken));
			}
			else
			{
				return new(Unsafe.As<Session>(_currentSessionOrTaskCompletionSource));
			}
		}
	}

	private async IAsyncEnumerable<TRequest> ProcessRequests<TRequest, TResponse>(Guid sessionId, IAsyncEnumerable<TResponse> responses, [EnumeratorCancellation] CancellationToken cancellationToken)
		where TRequest : IRequestId
		where TResponse : IRequestId
	{
		Session? session;
		ServiceState<TRequest, TResponse> service;
		lock (_lock)
		{
			if (!_sessions.TryGetValue(sessionId, out session))
			{
				_sessions.Add(sessionId, session = new Session(this, sessionId));
			}
			service = session.InitializeService<TRequest, TResponse>(responses);
		}
		await using (service)
		{
			await session.WaitAsync(cancellationToken).ConfigureAwait(false);
			await foreach (var request in service.EnumerateAllRequestsAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return request;
			}
		}
	}

	IAsyncEnumerable<AdapterRequest> IMonitorControlProxyService.ProcessAdapterRequestsAsync(Guid sessionId, IAsyncEnumerable<AdapterResponse> responses, CancellationToken cancellationToken)
		=> ProcessRequests<AdapterRequest, AdapterResponse>(sessionId, responses, cancellationToken);

	IAsyncEnumerable<MonitorRequest> IMonitorControlProxyService.ProcessMonitorRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorResponse> responses, CancellationToken cancellationToken)
		=> ProcessRequests<MonitorRequest, MonitorResponse>(sessionId, responses, cancellationToken);

	IAsyncEnumerable<MonitorCapabilitiesRequest> IMonitorControlProxyService.ProcessCapabilitiesRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorCapabilitiesResponse> responses, CancellationToken cancellationToken)
		=> ProcessRequests<MonitorCapabilitiesRequest, MonitorCapabilitiesResponse>(sessionId, responses, cancellationToken);

	IAsyncEnumerable<MonitorVcpGetRequest> IMonitorControlProxyService.ProcessVcpGetRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorVcpGetResponse> responses, CancellationToken cancellationToken)
		=> ProcessRequests<MonitorVcpGetRequest, MonitorVcpGetResponse>(sessionId, responses, cancellationToken);

	IAsyncEnumerable<MonitorVcpSetRequest> IMonitorControlProxyService.ProcessVcpSetRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorVcpSetResponse> responses, CancellationToken cancellationToken)
		=> ProcessRequests<MonitorVcpSetRequest, MonitorVcpSetResponse>(sessionId, responses, cancellationToken);

	async IAsyncEnumerable<MonitorReleaseRequest> IMonitorControlProxyService.EnumerateMonitorsToReleaseAsync(Guid sessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		Session? session;
		var channel = Channel.CreateUnbounded<MonitorReleaseRequest>(UnboundedChannelOptions);
		lock (_lock)
		{
			if (!_sessions.TryGetValue(sessionId, out session))
			{
				_sessions.Add(sessionId, session = new Session(this, sessionId));
			}
			session.SetMonitorReleaseWriter(channel);
		}
		try
		{
			await session.WaitAsync(cancellationToken).ConfigureAwait(false);
			await foreach (var request in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				yield return request;
			}
		}
		finally
		{
			session.ClearMonitorReleaseWriter();
		}
	}

	async Task<IMonitorControlAdapter> IMonitorControlService.ResolveAdapterAsync(string deviceName, CancellationToken cancellationToken)
	{
		var session = await GetSessionAsync(cancellationToken).ConfigureAwait(false);

		var adapterService = session.AdapterService;

		var response = await adapterService.SendRequestAsync(new AdapterRequest() { RequestId = adapterService.GetRequestId(), DeviceName = deviceName }, cancellationToken).ConfigureAwait(false);

		ValidateResponseStatus(response.Status);

		return new Adapter(session, response.AdapterId);
	}
}
