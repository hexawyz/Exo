namespace Exo.Devices.Nzxt.Kraken;

[Flags]
internal enum LightingEffectFlags
{
	None = 0x00,
	Moving = 0x01,
	Reversed = 0x02,
	// This is used by a few effects such as covering marquee, but I have not yet determined what it does.
	Unknown1 = 0x04,
}
