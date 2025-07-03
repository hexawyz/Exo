namespace Exo.Service;

[Flags]
public enum LightCapabilities : byte
{
	None = 0b00000000,
	Brightness = 0b00000001,
	Temperature = 0b00000010,
	Hue = 0b00000100,
	Saturation = 0b00001000,
	Rgb = 0b00010000,
	AddressableRgb = 0b10000000,
}
