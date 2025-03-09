namespace Exo.Rpc;

internal sealed class PipeClosedException : Exception
{
	public PipeClosedException() : base("The pipe is not open.") { }
}
