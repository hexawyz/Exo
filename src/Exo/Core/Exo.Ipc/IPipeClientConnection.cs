using System.IO.Pipes;

namespace Exo.Ipc;

/// <summary>Defines the constructor for connections used in a pipe client.</summary>
/// <typeparam name="TConnection">The type of the object implementing this interface.</typeparam>
public interface IPipeClientConnection<TConnection>
	where TConnection : PipeClientConnection, IPipeClientConnection<TConnection>
{
	/// <summary>Creates a new connection instance.</summary>
	/// <remarks>Implementations must always pass the provided parameters to the constructor.</remarks>
	/// <param name="server">The client to which this connection is attached.</param>
	/// <param name="stream">The stream that is used for communications.</param>
	/// <returns></returns>
	abstract static TConnection Create(PipeClient<TConnection> client, NamedPipeClientStream stream);
}
