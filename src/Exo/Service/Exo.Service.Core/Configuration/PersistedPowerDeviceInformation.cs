namespace Exo.Service.Configuration;

[TypeId(0xF118B54F, 0xDA42, 0x4768, 0x98, 0x50, 0x80, 0x7C, 0xB7, 0x71, 0x36, 0x24)]
internal readonly struct PersistedPowerDeviceInformation
{
	public PowerDeviceFlags Capabilities { get; init; }
	public TimeSpan MinimumIdleTime { get; init; }
	public TimeSpan MaximumIdleTime { get; init; }
	public byte MinimumBrightness { get; init; }
	public byte MaximumBrightness { get; init; }
}
