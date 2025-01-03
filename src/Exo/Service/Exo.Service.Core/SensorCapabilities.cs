namespace Exo.Service;

[Flags]
internal enum SensorCapabilities : byte
{
	None = 0b00000000,
	Polled = 0b00000001,
	Streamed = 0b00000010,
}
