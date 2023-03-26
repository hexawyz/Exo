using System.Threading.Tasks;

namespace Exo.Service;

internal sealed class HidDriverCreationContext : IDriverCreationContext<HidDriverWrapper>
{
	private readonly IDriverRegistry _driverRegistry;
	private Optional<IDriverRegistry>? _nestedDriverRegistry;

	public HidDriverCreationContext(IDriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public Optional<IDriverRegistry> DriverRegistry => _nestedDriverRegistry ??= new OptionalNestedDriverRegistry(_driverRegistry);

	public HidDriverWrapper CompleteAndReset(Driver driver)
	{
		Optional<IDriverRegistry>? nestedDriverRegistry = _nestedDriverRegistry;
		if (nestedDriverRegistry is not null)
		{
			_nestedDriverRegistry = null;
			if (nestedDriverRegistry.IsDisposed)
			{
				nestedDriverRegistry = null;
			}
		}
		return new(driver, nestedDriverRegistry);
	}

	public ValueTask DisposeAndResetAsync()
	{
		Optional<IDriverRegistry>? nestedDriverRegistry = _nestedDriverRegistry;
		if (nestedDriverRegistry is not null)
		{
			_nestedDriverRegistry = null;
			nestedDriverRegistry.Dispose();
		}
		return ValueTask.CompletedTask;
	}
}
