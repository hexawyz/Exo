namespace Exo.Service;

internal readonly struct MouseDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required bool IsConnected { get; init; }
	public required DotsPerInch MaximumDpi { get; init; }
	public required MouseDpiCapabilities DpiCapabilities { get; init; }
	public required byte MinimumDpiPresetCount { get; init; }
	public required byte MaximumDpiPresetCount { get; init; }
}
