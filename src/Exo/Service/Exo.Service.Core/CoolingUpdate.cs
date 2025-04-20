using Exo.Cooling.Configuration;

namespace Exo.Service;

internal readonly struct CoolingUpdate(Guid deviceId, Guid coolerId, CoolingModeConfiguration coolingMode)
{
	public Guid DeviceId { get; } = deviceId;
	public Guid CoolerId { get; } = coolerId;
	public CoolingModeConfiguration CoolingMode { get; } = coolingMode;
}
