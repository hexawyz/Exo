using Exo.Discovery;
using Microsoft.Extensions.Logging;

namespace Exo.Debug;

public sealed class DebugDriverCreationContext : DriverCreationContext
{
	public DebugDriver? Driver { get; }

	internal DebugDriverCreationContext(DebugDriver? driver)
	{
		Driver = driver;
	}

	protected override INestedDriverRegistryProvider NestedDriverRegistryProvider => throw new NotSupportedException();
	public override ILoggerFactory LoggerFactory => throw new NotSupportedException();
}
