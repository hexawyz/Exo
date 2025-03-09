using System.Collections.Immutable;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Exo.Contracts.Ui.Overlay;
using Exo.Rpc;
using Exo.Utils;

namespace Exo.Overlay;

internal sealed class ExoHelperClientConnection : PipeClientConnection, IPipeClientConnection<ExoHelperClientConnection>
{
	private static readonly ImmutableArray<byte> GitCommitId = GitCommitHelper.GetCommitId(typeof(ExoHelperClientConnection).Assembly);

	public static ExoHelperClientConnection Create(PipeClient<ExoHelperClientConnection> client, NamedPipeClientStream stream)
		=> new(client, stream, ((ExoHelperPipeClient)client).OverlayRequestWriter);

	private readonly ChannelWriter<OverlayRequest> _overlayRequestWriter;

	private ExoHelperClientConnection(PipeClient client, NamedPipeClientStream stream, ChannelWriter<OverlayRequest> overlayRequestWriter) : base(client, stream)
	{
		_overlayRequestWriter = overlayRequestWriter;
	}

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		while (true)
		{
			int count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
			if (count == 0) return;
			// If the message processing does not indicate success, we can close the connection.
			if (!ProcessMessage(buffer.Span[..count])) return;
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
		case ExoHelperProtocolServerMessage.CustomMenuItemAdd:
		case ExoHelperProtocolServerMessage.CustomMenuItemRemove:
		case ExoHelperProtocolServerMessage.CustomMenuItemUpdate:
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
}

internal sealed class ExoHelperPipeClient : PipeClient<ExoHelperClientConnection>
{
	internal ChannelWriter<OverlayRequest> OverlayRequestWriter { get; }

	public ExoHelperPipeClient(string pipeName, ChannelWriter<OverlayRequest> overlayRequestWriter) : base(pipeName, PipeTransmissionMode.Message)
	{
		OverlayRequestWriter = overlayRequestWriter;
	}
}
