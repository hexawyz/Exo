using System;
using System.Collections.Immutable;

namespace Exo;

public interface ISystemDeviceDriver : IAsyncDisposable
{
	/// <summary>Gets the friendly name associated with this instance.</summary>
	/// <remarks>
	/// The friendly name should be something that can be easily displayed within a User Interface.
	/// When possible, this friendly name should help identifying the device itself. (e.g. "Brand XXX Mouse Model YYY Version ZZZ")
	/// </remarks>
	string FriendlyName { get; }

	/// <summary>Gets the device names associated with this instance of the driver.</summary>
	/// <remarks>A driver can cover multiple device interfaces for a given device</remarks>
	/// <returns></returns>
	ImmutableArray<string> DeviceNames { get; }
}
