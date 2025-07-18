namespace Exo.Service;

public readonly struct DisconnectedLightingDeviceInformation(Guid deviceId)
{
	public Guid DeviceId { get; } = deviceId;
}
