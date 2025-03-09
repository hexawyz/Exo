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
		return new(client, stream, helperPipeClient.OverlayRequestWriter, helperPipeClient.MenuChannel);
	}

	private readonly ChannelWriter<OverlayRequest> _overlayRequestWriter;
	private readonly ResettableChannel<MenuChangeNotification> _menuChannel;

	private ExoHelperClientConnection
	(
		PipeClient client,
		NamedPipeClientStream stream,
		ChannelWriter<OverlayRequest> overlayRequestWriter,
		ResettableChannel<MenuChangeNotification> menuChannel
	) : base(client, stream)
	{
		_overlayRequestWriter = overlayRequestWriter;
		_menuChannel = menuChannel;
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
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorAcquireRequest:
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorReleaseRequest:
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorCapabilitiesRequest:
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpGetRequest:
		case ExoHelperProtocolServerMessage.MonitorProxyMonitorVcpSetRequest:
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
		if (data.Length < 18) throw new ArgumentException();

		byte deviceNameLength = data[17];

		if (data.Length < 18 + deviceNameLength) throw new ArgumentException();

		_overlayRequestWriter.TryWrite
		(
			new()
			{
				NotificationKind = (OverlayNotificationKind)data[0],
				Level = Unsafe.ReadUnaligned<uint>(in data[1]),
				MaxLevel = Unsafe.ReadUnaligned<uint>(in data[5]),
				Value = Unsafe.ReadUnaligned<long>(in data[9]),
				DeviceName = deviceNameLength > 0 ? Encoding.UTF8.GetString(data.Slice(18, deviceNameLength)) : null,
			}
		);
	}

	private void ProcessCustomMenu(WatchNotificationKind kind, ReadOnlySpan<byte> data)
	{
		var writer = _menuChannel.CurrentWriter;

		if (data.Length < 37) throw new ArgumentException();

		var type = (MenuItemType)data[36];
		byte textLength = 0;

		if (type is MenuItemType.Default or MenuItemType.SubMenu)
		{
			if (data.Length < 38) throw new ArgumentException();
			textLength = data[37];
			if (data.Length < 38 + textLength) throw new ArgumentException();
		}

		writer.TryWrite
		(
			new()
			{
				Kind = kind,
				ParentItemId = Unsafe.ReadUnaligned<Guid>(in data[0]),
				Position = Unsafe.ReadUnaligned<uint>(in data[16]),
				ItemId = Unsafe.ReadUnaligned<Guid>(in data[20]),
				ItemType = type,
				Text = textLength > 0 ? Encoding.UTF8.GetString(data.Slice(38, textLength)) : null
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
}

internal sealed class ExoHelperPipeClient : PipeClient<ExoHelperClientConnection>, IMenuItemInvoker
{
	internal ChannelWriter<OverlayRequest> OverlayRequestWriter { get; }
	internal ResettableChannel<MenuChangeNotification> MenuChannel { get; }

	public ExoHelperPipeClient
	(
		string pipeName,
		ChannelWriter<OverlayRequest> overlayRequestWriter,
		ResettableChannel<MenuChangeNotification> menuChannel) : base(pipeName, PipeTransmissionMode.Message
	)
	{
		OverlayRequestWriter = overlayRequestWriter;
		MenuChannel = menuChannel;
	}

	public async ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken)
	{
		if (CurrentConnection is { } connection)
		{
			await connection.InvokeMenuItemAsync(menuItemId, cancellationToken);
		}
	}
}
