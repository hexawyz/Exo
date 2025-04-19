namespace Exo.Service;

public readonly struct LightChangeNotification(Guid deviceId, Guid lightId, bool isOn, byte brightness, uint temperature)
{
	public Guid DeviceId { get; } = deviceId;
	public Guid LightId { get; } = lightId;
	public bool IsOn { get; } = isOn;
	public byte Brightness { get; } = brightness;
	public uint Temperature { get; } = temperature;
}
