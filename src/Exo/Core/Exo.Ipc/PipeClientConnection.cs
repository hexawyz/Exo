using System.IO.Pipes;
using Microsoft.Extensions.Logging;

namespace Exo.Ipc;

/// <summary>Base implementation for pipe clients.</summary>
/// <remarks>
/// Derived classes are assumed to provide the write methods using the protected members exposed by the class.
/// All Write methods must:
/// 1. Be asynchronous
/// 2. Request a CancellationTokenSource for writing by calling CreateWriteCancellationTokenSource. (This assumes that most writes will be called by passing an external cancellation)
/// 3. Acquire the write lock
/// 4. Ideally use the pre-allocated write buffer (which is protected by the lock) to build messages
/// 5. Call the internal <see cref="WriteAsync(ReadOnlyMemory{byte}, CancellationToken)"/> method.
/// </remarks>
public abstract class PipeClientConnection : PipeConnection
{
	protected PipeClientConnection(ILogger<PipeClientConnection> logger, PipeClient client, NamedPipeClientStream stream)
		: base(logger, client.Buffers, stream, client.CancellationToken)
	{
	}
}
