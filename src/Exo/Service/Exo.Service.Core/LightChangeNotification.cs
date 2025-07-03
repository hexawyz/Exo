namespace Exo.Service;

public readonly struct LightChangeNotification(Guid deviceId, Guid lightId, bool isOn, byte brightness, uint temperature, ushort hue, byte saturation)
{
	public Guid DeviceId { get; } = deviceId;
	public Guid LightId { get; } = lightId;
	public bool IsOn { get; } = isOn;
	public byte Brightness { get; } = brightness;
	public uint Temperature { get; } = temperature;
	public ushort Hue { get; } = hue;
	public byte Saturation { get; } = saturation;
}
