using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace Exo.Ipc;

/// <summary>Base implementation for pipe server connections.</summary>
/// <remarks>Rules are similar to the ones for <see cref="PipeClient"/>, just on the opposite side of the communication.</remarks>
public abstract class PipeServerConnection : PipeConnection
{
	protected PipeServerConnection(ILogger<PipeServerConnection> logger, PipeServer server, NamedPipeServerStream stream)
		: base(logger, stream, server.CancellationToken)
	{
	}
}
