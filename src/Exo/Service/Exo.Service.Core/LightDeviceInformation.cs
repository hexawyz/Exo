using System.Collections.Immutable;

namespace Exo.Service;

public readonly struct LightDeviceInformation(Guid deviceId, LightDeviceCapabilities capabilities, ImmutableArray<LightInformation> lights)
{
	public Guid DeviceId { get; } = deviceId;
	public LightDeviceCapabilities Capabilities { get; } = capabilities;
	public ImmutableArray<LightInformation> Lights { get; } = lights;
}
