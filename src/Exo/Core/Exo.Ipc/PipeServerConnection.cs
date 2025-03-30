using System.IO.Pipes;

namespace Exo.Ipc;

/// <summary>Base implementation for pipe server connections.</summary>
/// <remarks>Rules are similar to the ones for <see cref="PipeClient"/>, just on the opposite side of the communication.</remarks>
public abstract class PipeServerConnection : PipeConnection
{
	protected PipeServerConnection(PipeServer server, NamedPipeServerStream stream)
		: base(stream, server.CancellationToken)
	{
	}
}
