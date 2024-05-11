using System.Collections.Immutable;
using Exo.Cooling;

namespace Exo.Features.Cooling;

/// <summary>Allows cooling devices to expose their cooling controls.</summary>
public interface ICoolingControllerFeature : ICoolingDeviceFeature
{
	/// <summary>Gets the coolers managed by this device.</summary>
	ImmutableArray<ICooler> Coolers { get; }
	/// <summary>Applies all changes made to individual coolers since the last time.</summary>
	ValueTask ApplyChangesAsync();
}
