using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Exo.Service;

internal sealed class HidDriverWrapper : IDriverCreationResult, ISystemDeviceDriver
{
	public Driver Driver { get; }
	private readonly Optional<IDriverRegistry>? _nestedDriverRegistry;

	public HidDriverWrapper(Driver driver, Optional<IDriverRegistry>? nestedDriverRegistry)
	{
		Driver = driver;
		_nestedDriverRegistry = nestedDriverRegistry;
	}

	public string FriendlyName => Driver.FriendlyName;

	public ImmutableArray<string> DeviceNames => ((ISystemDeviceDriver)Driver).DeviceNames;

	public async ValueTask DisposeAsync()
	{
		await ((ISystemDeviceDriver)Driver).DisposeAsync().ConfigureAwait(false);
		_nestedDriverRegistry?.Dispose();
	}
}
