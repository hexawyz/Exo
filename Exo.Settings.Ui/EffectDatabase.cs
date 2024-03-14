using System;
using System.Collections.Generic;

namespace Exo.Settings.Ui;

internal class EffectDatabase
{
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> EffectNames = new()
	{
		{ new(0xC771A454, 0xCAE5, 0x41CF, 0x91, 0x21, 0xBE, 0xF8, 0xAD, 0xC3, 0x80, 0xED), "(Not Applicable)" },
		{ new(0x6B972C66, 0x0987, 0x4A0F, 0xA2, 0x0F, 0xCB, 0xFC, 0x1B, 0x0F, 0x3D, 0x4B), "Disabled" },
		{ new(0x2A30CB46, 0x8BF2, 0x4F0E, 0x98, 0x35, 0x77, 0x4E, 0xB0, 0x2D, 0x24, 0x8D), "Static Color" },
		{ new(0xED8D205B, 0xE693, 0x48C0, 0x8E, 0xC3, 0x72, 0x03, 0xF7, 0x67, 0x20, 0x2F), "Static Brightness" },
		{ new(0xC2738C37, 0xE2E3, 0x4686, 0xB1, 0x6B, 0x30, 0x7A, 0x68, 0x3B, 0xA1, 0xA6), "Color Pulse" },
		{ new(0x433FC57B, 0x6486, 0x48EA, 0x8F, 0xA1, 0x1D, 0x2A, 0x93, 0xE1, 0x92, 0xCB), "Color Pulse (Speed)" },
		{ new(0x02531B0C, 0xF13D, 0x4F0F, 0xAC, 0xFD, 0xC3, 0xF0, 0x88, 0x71, 0xA2, 0x7F), "Color Pulse (Advanced)" },
		{ new(0xA3F80010, 0x5663, 0x4ECF, 0x9C, 0x22, 0x03, 0x6E, 0x28, 0x47, 0x8B, 0x7E), "Color Flash" },
		{ new(0xF786DD5A, 0x83A7, 0x4B07, 0x91, 0x66, 0xD2, 0x31, 0xEF, 0x2D, 0xDE, 0x11), "Color Flash (Speed)" },
		{ new(0x99D1CFC4, 0xB25D, 0x4EEF, 0xAB, 0x17, 0x05, 0x2D, 0xF3, 0xD4, 0x93, 0x2D), "Color Flash (Advanced)" },
		{ new(0x2C497719, 0x8477, 0x4FE2, 0x80, 0x45, 0x48, 0x89, 0x39, 0xD4, 0xC9, 0x13), "Color Double Flash" },
		{ new(0x08B2116E, 0xCD5C, 0x4990, 0xA8, 0x73, 0x09, 0x14, 0xD6, 0x39, 0x17, 0x37), "Color Double Flash (Speed)" },
		{ new(0x2818D561, 0x15FB, 0x43B0, 0x9C, 0x2E, 0x9F, 0xF5, 0x08, 0x82, 0x2B, 0x7A), "Color Cycle" },
		{ new(0x712094B5, 0xC5B9, 0x4A2B, 0x96, 0x19, 0xE3, 0x3F, 0xB0, 0x49, 0xEE, 0x9F), "Color Cycle (Speed)" },
		{ new(0xB93254E0, 0xD39C, 0x40DF, 0xBF, 0x1F, 0x89, 0xD6, 0xCE, 0xB6, 0x16, 0x15), "Color Wave" },
		{ new(0xD11B8022, 0x2C92, 0x467A, 0xB8, 0x63, 0x9B, 0x70, 0x3D, 0x26, 0x5A, 0x70), "Color Wave (Speed)" },
		{ new(0x9FD48D3C, 0x9BD5, 0x403E, 0x8F, 0xFD, 0x8F, 0xF6, 0x6F, 0x47, 0xB2, 0x32), "Color Chase" },
		{ new(0xA4FBC975, 0x3CBF, 0x48AA, 0x9B, 0xFB, 0x1F, 0x12, 0x89, 0xE7, 0xC3, 0xA0), "Color Chase (Speed)" },
		{ new(0xA175E0AD, 0xF649, 0x4F10, 0x99, 0xE2, 0xC3, 0xC9, 0x4D, 0x1C, 0x9B, 0xC7), "Reactive" },


		{ new(0xB7CE4E5E, 0x4983, 0x4D3B, 0xA0, 0xD1, 0xEE, 0xB2, 0x28, 0x28, 0x37, 0x76), "Random Color Pulses" },
		{ new(0x45E154D6, 0x1946, 0x4215, 0xA4, 0x4F, 0x97, 0x5C, 0xB7, 0x6D, 0xEE, 0xAE), "Two Color Pulses" },

		{ new(0x85397AFD, 0xFFC7, 0x4C0C, 0x91, 0x45, 0x6D, 0xF9, 0x1D, 0x55, 0x68, 0x66), "Static Color 1" },
		{ new(0x52F39EE8, 0xB4CD, 0x492B, 0xB9, 0x9B, 0xF4, 0x9A, 0xBB, 0x4C, 0xFB, 0x71), "Static Color 2" },
		{ new(0x511BA640, 0x295B, 0x425D, 0xB1, 0x8A, 0xEB, 0xED, 0x49, 0xC0, 0xA6, 0x48), "Static Color 3" },
		{ new(0x2959B1E6, 0xC0D4, 0x4D5B, 0xA8, 0x0B, 0xC0, 0x6D, 0x7E, 0x2C, 0x4B, 0x74), "Static Color 4" },

		{ new(0x2C64145E, 0x84E2, 0x42AB, 0xA5, 0xAC, 0xC6, 0x3E, 0x4B, 0x40, 0x3E, 0xDC), "Static Colors" }, // 5 Static Colors
		{ new(0x476F95EB, 0x1D55, 0x46D6, 0xA0, 0x97, 0x9F, 0x9C, 0xC3, 0x4B, 0x56, 0xE5), "Static Colors" }, // 8 Static Colors

	};

	public static string? GetEffectDisplayName(Guid effectId)
		=> EffectNames.TryGetValue(effectId, out string name) ? name : null;
}
