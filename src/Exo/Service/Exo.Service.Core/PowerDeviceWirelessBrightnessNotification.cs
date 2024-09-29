namespace Exo.Service;

internal readonly struct PowerDeviceWirelessBrightnessNotification
{
	public required Guid DeviceId { get; init; }
	public required byte Brightness { get; init; }
}
