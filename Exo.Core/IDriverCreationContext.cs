using System.Threading.Tasks;

namespace Exo;

public interface IDriverCreationContext<TResult>
	where TResult : IDriverCreationResult
{
	TResult CompleteAndReset(Driver driver);
	ValueTask DisposeAndResetAsync();
}
