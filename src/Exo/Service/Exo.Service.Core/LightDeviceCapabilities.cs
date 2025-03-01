namespace Exo.Service;

[Flags]
public enum LightDeviceCapabilities : byte
{
	None = 0b00000000,
	Polled = 0b00000001,
}
