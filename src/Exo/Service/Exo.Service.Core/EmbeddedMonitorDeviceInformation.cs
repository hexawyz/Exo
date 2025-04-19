using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct EmbeddedMonitorDeviceInformation(Guid deviceId, ImmutableArray<EmbeddedMonitorInformation> embeddedMonitors)
{
	public Guid DeviceId { get; } = deviceId;
	public ImmutableArray<EmbeddedMonitorInformation> EmbeddedMonitors { get; } = embeddedMonitors;
}
