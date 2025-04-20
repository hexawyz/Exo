namespace Exo.Service.Ipc;

internal sealed class MaximumDepthExceededException : Exception
{
	public MaximumDepthExceededException() : this("The maximum depth level has been reached.")
	{
	}

	public MaximumDepthExceededException(string? message) : base(message) { }
}
