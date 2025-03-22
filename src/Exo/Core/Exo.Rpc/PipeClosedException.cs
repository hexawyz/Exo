namespace Exo.Rpc;

public sealed class PipeClosedException : Exception
{
	public PipeClosedException() : base("The pipe is not open.") { }
}
