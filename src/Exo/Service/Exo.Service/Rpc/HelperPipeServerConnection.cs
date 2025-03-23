using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Contracts.Ui.Overlay;
using Exo.Rpc;

namespace Exo.Service.Rpc;

internal sealed class HelperPipeServerConnection : PipeServerConnection, IPipeServerConnection<HelperPipeServerConnection>
{
	public static HelperPipeServerConnection Create(PipeServer<HelperPipeServerConnection> server, NamedPipeServerStream stream)
	{
		var helperPipeServer = (HelperPipeServer)server;
		return new(server, stream, helperPipeServer.OverlayNotificationService, helperPipeServer.CustomMenuService, helperPipeServer.MonitorControlProxyService);
	}

	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly CustomMenuService _customMenuService;
	private readonly DisposableChannel<MonitorControlProxyResponse, MonitorControlProxyRequest> _monitorControlProxyChannel;
	private int _state;

	private HelperPipeServerConnection
	(
		PipeServer server,
		NamedPipeServerStream stream,
		OverlayNotificationService overlayNotificationService,
		CustomMenuService customMenuService,
		MonitorControlProxyService monitorControlProxyService
	) : base(server, stream)
	{
		_overlayNotificationService = overlayNotificationService;
		_customMenuService = customMenuService;
		using (var callingProcess = Process.GetProcessById(NativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle)))
		{
			if (callingProcess.ProcessName != "Exo.Overlay")
			{
				throw new UnauthorizedAccessException("The client is not authorized.");
			}
		}
		_monitorControlProxyChannel = monitorControlProxyService.CreateChannel();
	}

	protected override ValueTask OnDisposedAsync() => _monitorControlProxyChannel.DisposeAsync();

	private async Task WatchEventsAsync(CancellationToken cancellationToken)
	{
		var overlayWatchTask = WatchOverlayRequestsAsync(cancellationToken);
		var customMenuWatchTask = WatchCustomMenuChangesAsync(cancellationToken);
		var monitorControlProxyRequestWatchTask = WatchMonitorControlProxyRequestsAsync(cancellationToken);

		await Task.WhenAll(overlayWatchTask, customMenuWatchTask, monitorControlProxyRequestWatchTask).ConfigureAwait(false);
	}

	private async Task WatchOverlayRequestsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var request in _overlayNotificationService.WatchOverlayRequestsAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = FillBuffer(buffer.Span, request);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}

				static int FillBuffer(Span<byte> buffer, OverlayRequest request)
				{
					buffer[0] = (byte)ExoHelperProtocolServerMessage.Overlay;
					return WriteRequest(buffer[1..], request) + 1;
				}

				static int WriteRequest(Span<byte> buffer, OverlayRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write((byte)request.NotificationKind);
					writer.Write(request.Level);
					writer.Write(request.MaxLevel);
					writer.Write(request.Value);
					writer.WriteVariableString(request.DeviceName ?? "");
					return (int)writer.Length;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task WatchCustomMenuChangesAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var notification in _customMenuService.WatchChangesAsync(cancellationToken).ConfigureAwait(false))
			{
				var message = notification.Kind switch
				{
					WatchNotificationKind.Enumeration => ExoHelperProtocolServerMessage.CustomMenuItemEnumeration,
					WatchNotificationKind.Addition => ExoHelperProtocolServerMessage.CustomMenuItemAdd,
					WatchNotificationKind.Removal => ExoHelperProtocolServerMessage.CustomMenuItemRemove,
					WatchNotificationKind.Update => ExoHelperProtocolServerMessage.CustomMenuItemUpdate,
					_ => throw new UnreachableException()
				};
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = FillBuffer(buffer.Span, message, notification);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}

				static int FillBuffer(Span<byte> buffer, ExoHelperProtocolServerMessage message, MenuItemWatchNotification notification)
				{
					buffer[0] = (byte)message;
					return WriteNotificationData(buffer[1..], notification) + 1;
				}

				static int WriteNotificationData(Span<byte> buffer, MenuItemWatchNotification notification)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(notification.ParentItemId);
					writer.Write(notification.Position);
					writer.Write(notification.MenuItem.ItemId);
					writer.Write((byte)notification.MenuItem.Type);
					if (notification.MenuItem.Type is Contracts.Ui.MenuItemType.Default or Contracts.Ui.MenuItemType.SubMenu)
					{
						writer.WriteVariableString((notification.MenuItem as TextMenuItem)?.Text ?? "");
					}
					return (int)writer.Length;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private async Task WatchMonitorControlProxyRequestsAsync(CancellationToken cancellationToken)
	{
		try
		{
			await foreach (var request in _monitorControlProxyChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
			{
				using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					var buffer = WriteBuffer;
					int count = FillBuffer(buffer.Span, request);
					await WriteAsync(buffer[..count], cancellationToken).ConfigureAwait(false);
				}

				static int FillBuffer(Span<byte> buffer, MonitorControlProxyRequest request)
				{
					ref byte message = ref buffer[0];
					var data = buffer[1..];
					switch (request.RequestType)
					{
					case MonitorControlProxyRequestResponseOneOfCase.Adapter:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyAdapterRequest;
						return WriteAdapterRequest(data, (AdapterRequest)request) + 1;
					case MonitorControlProxyRequestResponseOneOfCase.MonitorAcquire:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyMonitorAcquireRequest;
						return WriteMonitorAcquireRequest(data, (MonitorAcquireRequest)request) + 1;
					case MonitorControlProxyRequestResponseOneOfCase.MonitorRelease:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyMonitorReleaseRequest;
						return WriteMonitorReleaseRequest(data, (MonitorReleaseRequest)request) + 1;
					case MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyMonitorCapabilitiesRequest;
						return WriteMonitorCapabilitiesRequest(data, (MonitorCapabilitiesRequest)request) + 1;
					case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpGetRequest;
						return WriteMonitorVcpGetRequest(data, (MonitorVcpGetRequest)request) + 1;
					case MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet:
						message = (byte)ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpSetRequest;
						return WriteMonitorVcpSetRequest(data, (MonitorVcpSetRequest)request) + 1;
					default:
						throw new UnreachableException();
					}
				}

				static int WriteAdapterRequest(Span<byte> buffer, AdapterRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.WriteVariableString(request.DeviceName);
					return (int)writer.Length;
				}

				static int WriteMonitorAcquireRequest(Span<byte> buffer, MonitorAcquireRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.Write(request.AdapterId);
					writer.Write(request.EdidVendorId);
					writer.Write(request.EdidProductId);
					writer.Write(request.IdSerialNumber);
					writer.WriteVariableString(request.SerialNumber ?? "");
					return (int)writer.Length;
				}

				static int WriteMonitorReleaseRequest(Span<byte> buffer, MonitorReleaseRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.Write(request.MonitorHandle);
					return (int)writer.Length;
				}

				static int WriteMonitorCapabilitiesRequest(Span<byte> buffer, MonitorCapabilitiesRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.Write(request.MonitorHandle);
					return (int)writer.Length;
				}

				static int WriteMonitorVcpGetRequest(Span<byte> buffer, MonitorVcpGetRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.Write(request.MonitorHandle);
					writer.Write(request.VcpCode);
					return (int)writer.Length;
				}

				static int WriteMonitorVcpSetRequest(Span<byte> buffer, MonitorVcpSetRequest request)
				{
					var writer = new BufferWriter(buffer);
					writer.Write(request.RequestId);
					writer.Write(request.MonitorHandle);
					writer.Write(request.VcpCode);
					writer.Write(request.Value);
					return (int)writer.Length;
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// This should act as the handshake.
		try
		{
			await SendGitCommitIdAsync(cancellationToken).ConfigureAwait(false);
			using (var watchCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				Task? watchTask = null;
				{
					int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
					if (count == 0) return;
					if (!ProcessMessage(buffer.Span[..count])) return;
					if (_state > 0) watchTask = WatchEventsAsync(watchCancellationTokenSource.Token);
				}

				try
				{
					while (true)
					{
						int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
						if (count == 0) return;
						// Ignore all messages if the state is negative (it means that something wrong happened, likely that the SHA1 don't match)
						if (_state < 0) continue;
						// If the message processing does not indicate success, we can close the connection.
						if (!ProcessMessage(buffer.Span[..count])) return;
					}
				}
				catch (Exception ex)
				{
					// TODO: Log
				}
				finally
				{
					watchCancellationTokenSource.Cancel();
					if (watchTask is not null) await watchTask.ConfigureAwait(false);
				}
			}
		}
		finally
		{
			await _monitorControlProxyChannel.DisposeAsync();
		}
	}

	private Task SendGitCommitIdAsync(CancellationToken cancellationToken)
		=> Program.GitCommitId.IsDefault ?
			SendGitCommitIdAsync(ImmutableCollectionsMarshal.AsImmutableArray(new byte[20]), cancellationToken) :
			SendGitCommitIdAsync(Program.GitCommitId, cancellationToken);

	private async Task SendGitCommitIdAsync(ImmutableArray<byte> version, CancellationToken cancellationToken)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using (await WriteLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoHelperProtocolServerMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private bool ProcessMessage(ReadOnlySpan<byte> data) => ProcessMessage((ExoHelperProtocolClientMessage)data[0], data[1..]);

	private bool ProcessMessage(ExoHelperProtocolClientMessage message, ReadOnlySpan<byte> data)
	{
		if (_state == 0 && message != ExoHelperProtocolClientMessage.GitVersion) return false;
		switch (message)
		{
		case ExoHelperProtocolClientMessage.NoOp:
			return true;
		case ExoHelperProtocolClientMessage.GitVersion:
			if (data.Length != 20) return false;
			_state = Program.GitCommitId.IsDefaultOrEmpty || !data.SequenceEqual(ImmutableCollectionsMarshal.AsArray(Program.GitCommitId)!) ? -1 : 1;
			return true;
		case ExoHelperProtocolClientMessage.InvokeMenuCommand:
			if (data.Length != 16) return false;
			ProcessMenuItemInvocation(Unsafe.ReadUnaligned<Guid>(in data[0]));
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyErrorResponse:
			ProcessMonitorProxyErrorResponse(data);
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyAdapterResponse:
			ProcessAdapterResponse(data);
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorAcquireResponse:
			ProcessMonitorAcquireResponse(data);
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorCapabilitiesResponse:
			ProcessMonitorCapabilitiesResponse(data);
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorVcpGetResponse:
			ProcessMonitorVcpGetResponse(data);
			return true;
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorVcpSetResponse:
			ProcessMonitorVcpSetResponse(data);
			return true;
		}
		return false;
	}

	private void ProcessMenuItemInvocation(Guid commandId)
	{
	}

	private void ProcessMonitorProxyErrorResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new MonitorControlProxyErrorResponse(reader.Read<uint>(), (MonitorControlResponseStatus)reader.ReadByte()));
	}

	private void ProcessAdapterResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new AdapterResponse(reader.Read<uint>(), reader.Read<ulong>()));
	}

	private void ProcessMonitorAcquireResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new MonitorAcquireResponse(reader.Read<uint>(), reader.Read<uint>()));
	}

	private void ProcessMonitorCapabilitiesResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new MonitorCapabilitiesResponse(reader.Read<uint>(), ImmutableCollectionsMarshal.AsImmutableArray(reader.ReadVariableBytes())));
	}

	private void ProcessMonitorVcpGetResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new MonitorVcpGetResponse(reader.Read<uint>(), reader.Read<ushort>(), reader.Read<ushort>(), reader.ReadByte() != 0));
	}

	private void ProcessMonitorVcpSetResponse(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		TryWriteMonitorControlProxyResponse(new MonitorVcpSetResponse(reader.Read<uint>()));
	}

	private void TryWriteMonitorControlProxyResponse(MonitorControlProxyResponse response) => _monitorControlProxyChannel.Writer.TryWrite(response);
}
