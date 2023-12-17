namespace Exo.Programming;

[Flags]
public enum EventOptions : uint
{
	None = 0b00000000,
	IsLifetimeEvent = 0b00000001,
	IsModuleEvent = 0b00000010,
}
