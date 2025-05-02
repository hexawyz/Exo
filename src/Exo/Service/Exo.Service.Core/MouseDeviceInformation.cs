using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct MouseDeviceInformation(Guid deviceId, bool isConnected, MouseCapabilities capabilities, DotsPerInch maximumDpi, byte minimumDpiPresetCount, byte maximumDpiPresetCount, ImmutableArray<ushort> supportedPollingFrequencies)
{
	public Guid DeviceId { get; } = deviceId;
	public bool IsConnected { get; } = isConnected;
	public MouseCapabilities Capabilities { get; } = capabilities;
	public DotsPerInch MaximumDpi { get; } = maximumDpi;
	public byte MinimumDpiPresetCount { get; } = minimumDpiPresetCount;
	public byte MaximumDpiPresetCount { get; } = maximumDpiPresetCount;
	public ImmutableArray<ushort> SupportedPollingFrequencies { get; } = supportedPollingFrequencies;
}
