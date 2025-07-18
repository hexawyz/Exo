// I ideally would not rely on an external library to do the color conversions, but we already depend on ImageSharp for image stuff and that is unlikely to change. Might as well use it.
namespace Exo.Service;

[Flags]
public enum LightingCapabilities : byte
{
	None = 0b00000000,
	Brightness = 0b00000001,
	DeviceManagedLighting = 0b00000010,
	DynamicPresence = 0b00000100,
}
