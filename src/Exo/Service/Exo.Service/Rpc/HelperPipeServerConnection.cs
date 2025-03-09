using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Exo.Contracts.Ui.Overlay;
using Exo.Service;

namespace Exo.Rpc;

internal sealed class HelperPipeServerConnection : PipeServerConnection, IPipeServerConnection<HelperPipeServerConnection>
{
	public static HelperPipeServerConnection Create(PipeServer<HelperPipeServerConnection> server, NamedPipeServerStream stream)
		=> new(server, stream, ((HelperPipeServer)server).OverlayNotificationService, ((HelperPipeServer)server).CustomMenuService);

	private readonly OverlayNotificationService _overlayNotificationService;
	private readonly CustomMenuService _customMenuService;
	private int _state;

	private HelperPipeServerConnection(PipeServer server, NamedPipeServerStream stream, OverlayNotificationService overlayNotificationService, CustomMenuService customMenuService) : base(server, stream)
	{
		_overlayNotificationService = overlayNotificationService;
		_customMenuService = customMenuService;
	}

	private async Task WatchEventsAsync(CancellationToken cancellationToken)
	{
		var overlayWatchTask = WatchOverlayRequestsAsync(cancellationToken);
		var customMenuWatchTask = WatchCustomMenuChangesAsync(cancellationToken);

		await Task.WhenAll(overlayWatchTask, customMenuWatchTask).ConfigureAwait(false);
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
					int count;
					// TODO: Refactor to have a kind of writer struct able to write variable integers.
					buffer[0] = (byte)request.NotificationKind;
					Unsafe.WriteUnaligned(ref buffer[1], request.Level);
					Unsafe.WriteUnaligned(ref buffer[5], request.MaxLevel);
					Unsafe.WriteUnaligned(ref buffer[9], request.Value);
					count = 17;
					var text = (request.DeviceName ?? "").AsSpan();
					// TODO: Make the length into a var int later on. Truncate for now.
					if (text.Length > 63) text = text[0..63];
					count = count + 1 + (buffer[17] = (byte)Encoding.UTF8.GetBytes(text, buffer[18..]));
					return count;
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
					int count;
					// TODO: Refactor to have a kind of writer struct able to write variable integers.
					Unsafe.WriteUnaligned(ref buffer[0], notification.ParentItemId);
					Unsafe.WriteUnaligned(ref buffer[16], notification.Position);
					Unsafe.WriteUnaligned(ref buffer[20], notification.MenuItem.ItemId);
					buffer[36] = (byte)notification.MenuItem.Type;
					count = 37;
					if (notification.MenuItem.Type is Contracts.Ui.MenuItemType.Default or Contracts.Ui.MenuItemType.SubMenu)
					{
						var text = (notification.MenuItem as TextMenuItem)?.Text ?? "".AsSpan();
						// TODO: Make the length into a var int later on. Truncate for now.
						if (text.Length > 63) text = text[0..63];
						count = count + 1 + (buffer[37] = (byte)Encoding.UTF8.GetBytes(text, buffer[38..]));
					}
					return count;
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
			finally
			{
				watchCancellationTokenSource.Cancel();
				if (watchTask is not null) await watchTask.ConfigureAwait(false);
			}
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
		case ExoHelperProtocolClientMessage.MonitorProxyAdapterResponse:
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorAcquireResponse:
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorCapabilitiesResponse:
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorVcpGetResponse:
		case ExoHelperProtocolClientMessage.MonitorProxyMonitorVcpSetResponse:
			return true;
		}
		return false;
	}

	private void ProcessMenuItemInvocation(Guid commandId)
	{
	}
}
