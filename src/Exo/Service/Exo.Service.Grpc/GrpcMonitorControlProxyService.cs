using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
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
	private sealed class Session : IAsyncDisposable
	{
		private readonly Dictionary<uint, (MonitorControlProxyRequestResponseOneOfCase RequestType, object TaskCompletionSource)> _pendingRequests;
		private readonly GrpcMonitorControlProxyService _service;
		private readonly ChannelWriter<MonitorControlProxyRequest> _requests;
		private uint _requestId;

		private Session? _nextSession;

		private CancellationTokenSource? _cancellationTokenSource;
		private readonly Task _runTask;

		public Session(GrpcMonitorControlProxyService service, ChannelWriter<MonitorControlProxyRequest> requests, IAsyncEnumerable<MonitorControlProxyResponse> responses)
		{
			_service = service;
			_requests = requests;
			_pendingRequests = new();
			_cancellationTokenSource = new();
			_runTask = RunAsync(responses, _cancellationTokenSource.Token);
			_service.RegisterActiveSession(this);
		}

		public bool IsDisposed => _cancellationTokenSource is null;

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _cancellationTokenSource, null) is not { } cts) return;

			cts.Cancel();

			await _runTask.ConfigureAwait(false);
			_service.UnregisterSession(this);
			cts.Dispose();
		}

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

		private async Task RunAsync(IAsyncEnumerable<MonitorControlProxyResponse> responses, CancellationToken cancellationToken)
		{
			try
			{
				await foreach (var response in responses.ConfigureAwait(false))
				{
					lock (_pendingRequests)
					{
						if (_pendingRequests.Remove(response.RequestId, out var t))
						{
							var (requestType, tcs) = t;
							switch (requestType)
							{
							case MonitorControlProxyRequestResponseOneOfCase.Adapter:
								CompleteRequest(response.Status, response.Content.AdapterResponse, tcs);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.Monitor:
								CompleteRequest(response.Status, response.Content.MonitorResponse, tcs);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorRelease:
								CompleteRequest(response.Status, response.Content.MonitorReleaseResponse, tcs);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities:
								CompleteRequest(response.Status, response.Content.MonitorCapabilitiesResponse, tcs);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet:
								CompleteRequest(response.Status, response.Content.MonitorVcpGetResponse, tcs);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet:
								CompleteRequest(response.Status, response.Content.MonitorVcpSetResponse, tcs);
								break;
							default:
								// We control the type of requests that we send, so if this case is reached, this can only be a bug in the code. (Or a solar flare)
								throw new InvalidOperationException("Unhandled request type.");
							}
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
			finally
			{
				// We need to fail all the remaining requests so that the callers are notified of the disposal.
				lock (_pendingRequests)
				{
					_requests.TryComplete();
					if (_pendingRequests.Count > 0)
					{
						var exception = ExceptionDispatchInfo.SetCurrentStackTrace(new ObjectDisposedException(typeof(Session).FullName));
						foreach (var t in _pendingRequests.Values)
						{
							var (requestType, tcs) = t;
							switch (requestType)
							{
							case MonitorControlProxyRequestResponseOneOfCase.Adapter:
								FailRequest<AdapterResponse>(tcs, exception);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.Monitor:
								FailRequest<MonitorResponse>(tcs, exception);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorRelease:
								FailRequest<MonitorReleaseResponse>(tcs, exception);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities:
								FailRequest<MonitorCapabilitiesResponse>(tcs, exception);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet:
								FailRequest<MonitorVcpGetResponse>(tcs, exception);
								break;
							case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet:
								FailRequest<MonitorVcpSetResponse>(tcs, exception);
								break;
							}
						}
					}
				}
			}
		}

		private static void FailRequest<TResponse>(object tcs, Exception exception)
			where TResponse : class
			=> Unsafe.As<TaskCompletionSource<TResponse>>(tcs).TrySetException(exception);

		private static void CompleteRequest<TResponse>(MonitorControlResponseStatus status, TResponse? response, object tcs)
			where TResponse : class
			=> CompleteRequest(status, response, Unsafe.As<TaskCompletionSource<TResponse>>(tcs));

		private static void CompleteRequest<TResponse>(MonitorControlResponseStatus status, TResponse? response, TaskCompletionSource<TResponse> tcs)
			where TResponse : class
		{
			switch (status)
			{
			case MonitorControlResponseStatus.Success:
				if (response is not null)
				{
					tcs.SetResult(response);
				}
				else
				{
					tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidOperationException("The response was unexpectedly empty.")));
				}
				break;
			case MonitorControlResponseStatus.NotFound:
				tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new DeviceNotFoundException()));
				break;
			case MonitorControlResponseStatus.Error:
				tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("An error has occurred while processing the request.")));
				break;
			case MonitorControlResponseStatus.InvalidVcpCode:
				tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new VcpCodeNotSupportedException()));
				break;
			default:
				tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("An error occurred with an unknown status.")));
				break;
			}
		}

		public ValueTask<AdapterResponse> SendRequestAsync(AdapterRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<AdapterResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		public ValueTask<MonitorResponse> SendRequestAsync(MonitorRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<MonitorResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		public ValueTask<MonitorReleaseResponse> SendRequestAsync(MonitorReleaseRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<MonitorReleaseResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		public ValueTask<MonitorCapabilitiesResponse> SendRequestAsync(MonitorCapabilitiesRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<MonitorCapabilitiesResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		public ValueTask<MonitorVcpGetResponse> SendRequestAsync(MonitorVcpGetRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<MonitorVcpGetResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		public ValueTask<MonitorVcpSetResponse> SendRequestAsync(MonitorVcpSetRequest request, CancellationToken cancellationToken)
			=> SendRequestAsync<MonitorVcpSetResponse>(new MonitorControlProxyRequest() { RequestId = Interlocked.Increment(ref _requestId), Content = request }, cancellationToken);

		private async ValueTask<TResponse> SendRequestAsync<TResponse>(MonitorControlProxyRequest request, CancellationToken cancellationToken)
		{
			TaskCompletionSource<TResponse> tcs;
			uint requestId = request.RequestId;
			lock (_pendingRequests)
			{
				tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
				_pendingRequests.Add(requestId, (request.Content.RequestType, tcs));
			}
			try
			{
				await _requests.WriteAsync(request, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				lock (_pendingRequests)
				{
					_pendingRequests.Remove(requestId);
				}
				throw;
			}
			try
			{
				return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException ex) when (tcs.TrySetCanceled(ex.CancellationToken))
			{
				// Remove the request from the pending request dictionary if it wasn't already.
				lock (_pendingRequests)
				{
					_pendingRequests.Remove(requestId);
				}
				throw;
			}
		}
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
			var response = await _session.SendRequestAsync
			(
				new MonitorRequest()
				{
					AdapterId = _adapterId,
					EdidVendorId = vendorId,
					EdidProductId = productId,
					IdSerialNumber = idSerialNumber,
					SerialNumber = serialNumber,
				},
				cancellationToken
			).ConfigureAwait(false);
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

		async ValueTask IAsyncDisposable.DisposeAsync()
		{
			if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
			{
				try
				{
					await _session.SendRequestAsync(new MonitorReleaseRequest() { MonitorHandle = _monitorHandle }, default).ConfigureAwait(false);
				}
				catch
				{
				}
			}
		}

		private void EnsureNotDisposed()
		{
		Retry:;
			ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, typeof(Monitor));
			if (_session.IsDisposed)
			{
				Volatile.Write(ref _isDisposed, 1);
				goto Retry;
			}
		}

		async Task<ImmutableArray<byte>> IMonitorControlMonitor.GetCapabilitiesAsync(CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var response = await _session.SendRequestAsync(new MonitorCapabilitiesRequest() { MonitorHandle = _monitorHandle }, cancellationToken).ConfigureAwait(false);
			return response.Utf8Capabilities;
		}

		async Task<VcpFeatureReply> IMonitorControlMonitor.GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var response = await _session.SendRequestAsync(new MonitorVcpGetRequest() { MonitorHandle = _monitorHandle, VcpCode = vcpCode }, cancellationToken).ConfigureAwait(false);
			return new(response.CurrentValue, response.MaximumValue, response.IsMomentary);
		}

		async Task IMonitorControlMonitor.SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
		{
			EnsureNotDisposed();
			var response = await _session.SendRequestAsync(new MonitorVcpSetRequest() { MonitorHandle = _monitorHandle, VcpCode = vcpCode, Value = value }, cancellationToken).ConfigureAwait(false);
		}
	}

	private readonly object _lock = new();
	private object? _currentSessionOrTaskCompletionSource;

	private void RegisterActiveSession(Session session)
	{
		lock (_lock)
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
	}

	private void UnregisterSession(Session session)
	{
		lock (_lock)
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

	async Task<IMonitorControlAdapter> IMonitorControlService.ResolveAdapterAsync(string deviceName, CancellationToken cancellationToken)
	{
		var session = await GetSessionAsync(cancellationToken).ConfigureAwait(false);
		var response = await session.SendRequestAsync(new AdapterRequest() { DeviceName = deviceName }, cancellationToken).ConfigureAwait(false);
		return new Adapter(session, response.AdapterId);
	}

	async IAsyncEnumerable<MonitorControlProxyRequest> IMonitorControlProxyService.ProcessRequestsAsync(IAsyncEnumerable<MonitorControlProxyResponse> responses, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var requests = Channel.CreateBounded<MonitorControlProxyRequest>(BoundedChannelOptions);
		await using var session = new Session(this, requests, responses);
		await foreach (var request in requests.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return request;
		}
	}
}
