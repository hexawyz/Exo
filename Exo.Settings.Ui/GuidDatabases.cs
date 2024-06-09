namespace Exo.Settings.Ui;

internal static class EffectDatabase
{
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> EffectNames = new()
	{
		{ new(0x02531B0C, 0xF13D, 0x4F0F, 0xAC, 0xFD, 0xC3, 0xF0, 0x88, 0x71, 0xA2, 0x7F), "Pulse (Advanced)" },
		{ new(0x99D1CFC4, 0xB25D, 0x4EEF, 0xAB, 0x17, 0x05, 0x2D, 0xF3, 0xD4, 0x93, 0x2D), "Flash (Advanced)" },
		{ new(0x08B2116E, 0xCD5C, 0x4990, 0xA8, 0x73, 0x09, 0x14, 0xD6, 0x39, 0x17, 0x37), "Double Flash (Speed)" },
		{ new(0xBAB1B0F1, 0x55F3, 0x46F8, 0x9A, 0xDC, 0xE6, 0x98, 0x4B, 0x38, 0x19, 0x02), "Chase" }, // Aura RAM
		{ new(0x2CB30144, 0x2586, 0x4780, 0x95, 0x6C, 0x43, 0x19, 0x8A, 0xF2, 0x72, 0x6F), "Wave" }, // Aura RAM
		{ new(0xF6A8C369, 0xD230, 0x4E63, 0xB6, 0x00, 0xA4, 0x4F, 0x1B, 0x3B, 0xBE, 0xCA), "Spectrum Wave" }, // Aura RAM
		{ new(0x716E4BB3, 0x6725, 0x4D98, 0x86, 0x3B, 0xE8, 0xDD, 0xE8, 0xA7, 0x87, 0xB6), "Spectrum Cycle Pulse" },
		{ new(0xB815E8FD, 0x7AD5, 0x4A48, 0x8F, 0xDB, 0x04, 0x43, 0x1F, 0x15, 0x98, 0x53), "Spectrum Cycle Chase" },
		{ new(0x17325DE9, 0x9572, 0x41C7, 0xA1, 0xE7, 0x8D, 0x1B, 0x2A, 0x28, 0xEA, 0x30), "Wide Spectrum Cycle Chase" },
		{ new(0x36DE8ED5, 0x1783, 0x4EFC, 0xB3, 0x1A, 0xA9, 0xB0, 0xB6, 0x56, 0xB0, 0x9A), "Spectrum Cycle Wave" },
		{ new(0xEDD773B0, 0x727E, 0x4C12, 0xB9, 0x2A, 0xDA, 0x05, 0x5A, 0xCE, 0x49, 0x91), "Alternate Spectrums" },
		{ new(0x1FA781E9, 0x2426, 0x4F06, 0x9B, 0x6E, 0x72, 0x55, 0xEE, 0x02, 0xA4, 0x3A), "Sparkling Spectrum Cycle" },

	};

	public static string? GetEffectDisplayName(Guid effectId)
		=> EffectNames.TryGetValue(effectId, out string? name) ? name : null;
}
