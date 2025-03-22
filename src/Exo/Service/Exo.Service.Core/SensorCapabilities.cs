namespace Exo.Service;

[Flags]
internal enum SensorCapabilities : byte
{
	None = 0b00000000,
	Polled = 0b00000001,
	Streamed = 0b00000010,
	HasMinimumValue = 0b01000000,
	HasMaximumValue = 0b10000000,
}
