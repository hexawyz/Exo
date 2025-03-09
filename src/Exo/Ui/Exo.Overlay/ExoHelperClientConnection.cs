using System.Collections.Immutable;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Exo.Contracts.Ui;
using Exo.Contracts.Ui.Overlay;
using Exo.Rpc;
using Exo.Utils;

namespace Exo.Overlay;

internal sealed class ExoHelperClientConnection : PipeClientConnection, IPipeClientConnection<ExoHelperClientConnection>
{
	private static readonly ImmutableArray<byte> GitCommitId = GitCommitHelper.GetCommitId(typeof(ExoHelperClientConnection).Assembly);

	public static ExoHelperClientConnection Create(PipeClient<ExoHelperClientConnection> client, NamedPipeClientStream stream)
	{
		var helperPipeClient = (ExoHelperPipeClient)client;
		return new(client, stream, helperPipeClient.OverlayRequestWriter, helperPipeClient.MenuChannel, helperPipeClient.MonitorControlProxyRequestChannel);
	}

	private readonly ChannelWriter<OverlayRequest> _overlayRequestWriter;
	private readonly ResettableChannel<MenuChangeNotification> _menuChannel;
	private readonly ResettableChannel<MonitorControlProxyRequest> _monitorControlProxyRequestChannel;

	private ExoHelperClientConnection
	(
		PipeClient client,
		NamedPipeClientStream stream,
		ChannelWriter<OverlayRequest> overlayRequestWriter,
		ResettableChannel<MenuChangeNotification> menuChannel,
		ResettableChannel<MonitorControlProxyRequest> monitorControlProxyRequestChannel
	) : base(client, stream)
	{
		_overlayRequestWriter = overlayRequestWriter;
		_menuChannel = menuChannel;
		_monitorControlProxyRequestChannel = monitorControlProxyRequestChannel;
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		try
		{
			while (true)
			{
				int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
				if (count == 0) return;
				// If the message processing does not indicate success, we can close the connection.
				if (!ProcessMessage(buffer.Span[..count])) return;
			}
		}
		finally
		{
			_menuChannel.Reset();
			_monitorControlProxyRequestChannel.Reset();
		}
	}

	private bool ProcessMessage(ReadOnlySpan<byte> data) => ProcessMessage((ExoHelperProtocolServerMessage)data[0], data[1..]);

	private bool ProcessMessage(ExoHelperProtocolServerMessage message, ReadOnlySpan<byte> data)
	{
		switch (message)
		{
		case ExoHelperProtocolServerMessage.NoOp:
			return true;
		case ExoHelperProtocolServerMessage.GitVersion:
			if (data.Length != 20) return false;
#if DEBUG
			ConfirmVersion(data.ToImmutableArray());
#else
			if (GitCommitId.IsDefaultOrEmpty) ConfirmVersion(data.ToImmutableArray());
			else ConfirmVersion(GitCommitId);
#endif
			return true;
		case ExoHelperProtocolServerMessage.Overlay:
			ProcessOverlayRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.CustomMenuItemEnumeration:
			ProcessCustomMenu(WatchNotificationKind.Enumeration, data);
			return true;
		case ExoHelperProtocolServerMessage.CustomMenuItemAdd:
			ProcessCustomMenu(WatchNotificationKind.Addition, data);
			return true;
		case ExoHelperProtocolServerMessage.CustomMenuItemRemove:
			ProcessCustomMenu(WatchNotificationKind.Removal, data);
			return true;
		case ExoHelperProtocolServerMessage.CustomMenuItemUpdate:
			ProcessCustomMenu(WatchNotificationKind.Update, data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyAdapterRequest:
			ProcessAdapterRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorAcquireRequest:
			ProcessMonitorAcquireRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorReleaseRequest:
			ProcessMonitorReleaseRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorCapabilitiesRequest:
			ProcessMonitorCapabilitiesRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpGetRequest:
			ProcessMonitorVcpGetRequest(data);
			return true;
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpSetRequest:
			ProcessMonitorVcpSetRequest(data);
			return true;
		}
		return false;
	}

	private async void ConfirmVersion(ImmutableArray<byte> version)
	{
		if (version.IsDefault || version.Length != 20) throw new ArgumentException();

		using var cts = CreateWriteCancellationTokenSource(default);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..21];
			FillBuffer(buffer.Span, version);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, ImmutableArray<byte> version)
		{
			buffer[0] = (byte)ExoHelperProtocolClientMessage.GitVersion;
			ImmutableCollectionsMarshal.AsArray(version)!.CopyTo(buffer[1..]);
		}
	}

	private void ProcessOverlayRequest(ReadOnlySpan<byte> data)
	{
		var reader = new BufferReader(data);

		_overlayRequestWriter.TryWrite
		(
			new()
			{
				NotificationKind = (OverlayNotificationKind)reader.ReadByte(),
				Level = reader.Read<uint>(),
				MaxLevel = reader.Read<uint>(),
				Value = reader.Read<long>(),
				DeviceName = reader.ReadVariableString(),
			}
		);
	}

	private void ProcessCustomMenu(WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		var channelWriter = _menuChannel.CurrentWriter;
		var reader = new BufferReader(data);

		channelWriter.TryWrite
		(
			new()
			{
				Kind = kind,
				ParentItemId = reader.Read<Guid>(),
				Position = reader.Read<uint>(),
				ItemId = reader.Read<Guid>(),
				ItemType = (MenuItemType)reader.ReadByte(),
				Text = reader.RemainingLength > 0 ? reader.ReadVariableString() ?? "" : null
			}
		);
	}

	internal async ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		using var cts = CreateWriteCancellationTokenSource(cancellationToken);
		using (await WriteLock.WaitAsync(cts.Token).ConfigureAwait(false))
		{
			var buffer = WriteBuffer[0..17];
			FillBuffer(buffer.Span, menuItemId);
			await WriteAsync(buffer, cts.Token).ConfigureAwait(false);
		}

		static void FillBuffer(Span<byte> buffer, Guid menuItemId)
		{
			buffer[0] = (byte)ExoHelperProtocolClientMessage.InvokeMenuCommand;
			Unsafe.WriteUnaligned(ref buffer[1], menuItemId);
		}
	}

	private void ProcessAdapterRequest(ReadOnlySpan<byte> data)
	{
	}

	private void ProcessMonitorAcquireRequest(ReadOnlySpan<byte> data)
	{
	}

	private void ProcessMonitorReleaseRequest(ReadOnlySpan<byte> data)
	{
	}

	private void ProcessMonitorCapabilitiesRequest(ReadOnlySpan<byte> data)
	{
	}

	private void ProcessMonitorVcpGetRequest(ReadOnlySpan<byte> data)
	{
	}

	private void ProcessMonitorVcpSetRequest(ReadOnlySpan<byte> data)
	{
	}
}

internal sealed class ExoHelperPipeClient : PipeClient<ExoHelperClientConnection>, IMenuItemInvoker
{
	internal ChannelWriter<OverlayRequest> OverlayRequestWriter { get; }
	internal ResettableChannel<MenuChangeNotification> MenuChannel { get; }
	internal ResettableChannel<MonitorControlProxyRequest> MonitorControlProxyRequestChannel { get; }

	public ExoHelperPipeClient
	(
		string pipeName,
		ChannelWriter<OverlayRequest> overlayRequestWriter,
		ResettableChannel<MenuChangeNotification> menuChannel,
		ResettableChannel<MonitorControlProxyRequest> monitorControlProxyRequestChannel
	) : base(pipeName, PipeTransmissionMode.Message
	)
	{
		OverlayRequestWriter = overlayRequestWriter;
		MenuChannel = menuChannel;
		MonitorControlProxyRequestChannel = monitorControlProxyRequestChannel;
	}

	public async ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		if (CurrentConnection is { } connection)
		{
			await connection.InvokeMenuItemAsync(menuItemId, cancellationToken);
		}
	}
}
