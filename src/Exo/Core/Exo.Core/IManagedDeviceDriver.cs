using System.Threading.Tasks;

namespace Exo;

/// <summary>An interface exposing a way to instanciate a given driver class.</summary>
/// <remarks>
/// <para>
/// This interface only enforces the need of a factory method for a given manager. It is not necessary for this method to exist in order for a driver to be implemented.
/// Details on how to instanciate a driver are privy to the specific driver manager or the composite driver owning a child driver.
/// </para>
/// <para>
/// It would generally be a good idea, however, to follow this pattern when possible, as it allows drivers to first fetch device information before creating a valid instance.
/// </para>
/// </remarks>
/// <typeparam name="TDeviceManager">The type of device manager that should manage this driver.</typeparam>
public interface IManagedDeviceDriver<TDeviceManager>
{
	static abstract ValueTask<Driver> CreateAsync(TDeviceManager deviceManager, string path);
}
