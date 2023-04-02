using System.Threading.Tasks;
using DeviceTools;

namespace Exo.Service;

internal sealed class HidDriverCreationContext : IDriverCreationContext<HidDriverWrapper>
{
	private readonly IDriverRegistry _driverRegistry;
	private Optional<IDriverRegistry>? _nestedDriverRegistry;
	
	public DeviceId DeviceId { get; private set; }

	public ushort VendorId => DeviceId.VendorId;
	public ushort ProductId => DeviceId.ProductId;
	public ushort Version => DeviceId.Version;

	public HidDriverCreationContext(IDriverRegistry driverRegistry) => _driverRegistry = driverRegistry;

	public Optional<IDriverRegistry> DriverRegistry => _nestedDriverRegistry ??= new OptionalNestedDriverRegistry(_driverRegistry);

	public void Initialize(DeviceId deviceId)
	{
		DeviceId = deviceId;
	}

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
