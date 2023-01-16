using System.Collections.Immutable;

namespace Exo.Service;

public interface ISystemDeviceDriver
{
	/// <summary>Gets the device names associated with this instance of the driver.</summary>
	/// <remarks>A driver can cover multiple device interfaces for a given device</remarks>
	/// <returns></returns>
	ImmutableArray<string> GetDeviceNames();
}
