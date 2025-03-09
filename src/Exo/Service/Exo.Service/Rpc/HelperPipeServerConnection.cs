using System.Collections.Immutable;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Service;

namespace Exo.Rpc;

internal sealed class HelperPipeServerConnection : PipeServerConnection, IPipeServerConnection<HelperPipeServerConnection>
{
	public static HelperPipeServerConnection Create(PipeServer<HelperPipeServerConnection> server, NamedPipeServerStream stream) => new(server, stream);

	private int _state;

	private HelperPipeServerConnection(PipeServer server, NamedPipeServerStream stream) : base(server, stream) { }

	protected override async Task ReadAndProcessMessagesAsync(PipeStream stream, Memory<byte> buffer, CancellationToken cancellationToken)
	{
		// This should act as the handshake.
		await SendGitCommitIdAsync(cancellationToken).ConfigureAwait(false);
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
			buffer[0] = (byte)ExoHelperProtocolClientMessage.GitVersion;
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
			if (!(Program.GitCommitId.IsDefaultOrEmpty || !data.SequenceEqual(ImmutableCollectionsMarshal.AsArray(Program.GitCommitId)!))) _state = -1;
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
