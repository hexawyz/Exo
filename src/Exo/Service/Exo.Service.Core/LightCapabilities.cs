namespace Exo.Service;

[Flags]
public enum LightCapabilities : byte
{
	None = 0b00000000,
	Brightness = 0b00000001,
	Temperature = 0b00000010,
	Color = 0b00000100,
}
