using System;

namespace Exo;

public interface IDriverCreationResult : IAsyncDisposable
{
	Driver Driver { get; }
}
