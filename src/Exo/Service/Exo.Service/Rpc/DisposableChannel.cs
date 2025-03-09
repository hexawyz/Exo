using System.Threading.Channels;

namespace Exo.Service.Rpc;

internal abstract class DisposableChannel<TWrite, TRead> : Channel<TWrite, TRead>, IAsyncDisposable
{
	public abstract ValueTask DisposeAsync();
}
