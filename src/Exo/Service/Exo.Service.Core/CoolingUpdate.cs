using Exo.Cooling.Configuration;

namespace Exo.Service;

internal readonly struct CoolingUpdate
{
	public required Guid DeviceId { get; init; }
	public required Guid CoolerId { get; init; }
	public required CoolingModeConfiguration CoolingMode { get; init; }
}
