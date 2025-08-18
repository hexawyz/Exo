namespace Exo.Lighting;

[Flags]
public enum EffectCapabilities : byte
{
	None = 0b00000000,
	Programmable = 0b00000001,
	Dynamic = 0b00000010,
}
