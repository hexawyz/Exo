using Exo.Contracts;

namespace Exo.Service;

internal readonly struct PowerDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required bool IsConnected { get; init; }
	public required PowerDeviceCapabilities Capabilities { get; init; }
	public required TimeSpan MinimumIdleTime { get; init; }
	public required TimeSpan MaximumIdleTime { get; init; }
	public byte MinimumBrightness { get; init; }
	public byte MaximumBrightness { get; init; }
}
