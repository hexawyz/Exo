namespace Exo.Settings.Ui;

internal static class EffectDatabase
{
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> EffectNames = new()
	{
		{ new(0xC771A454, 0xCAE5, 0x41CF, 0x91, 0x21, 0xBE, 0xF8, 0xAD, 0xC3, 0x80, 0xED), "(Not Applicable)" },
		{ new(0x6B972C66, 0x0987, 0x4A0F, 0xA2, 0x0F, 0xCB, 0xFC, 0x1B, 0x0F, 0x3D, 0x4B), "Disabled" },
		{ new(0x2A30CB46, 0x8BF2, 0x4F0E, 0x98, 0x35, 0x77, 0x4E, 0xB0, 0x2D, 0x24, 0x8D), "Static" },
		{ new(0xED8D205B, 0xE693, 0x48C0, 0x8E, 0xC3, 0x72, 0x03, 0xF7, 0x67, 0x20, 0x2F), "Static" },
		{ new(0x2C64145E, 0x84E2, 0x42AB, 0xA5, 0xAC, 0xC6, 0x3E, 0x4B, 0x40, 0x3E, 0xDC), "Static" }, // 5 Colors
		{ new(0x476F95EB, 0x1D55, 0x46D6, 0xA0, 0x97, 0x9F, 0x9C, 0xC3, 0x4B, 0x56, 0xE5), "Static" }, // 8 Colors
		{ new(0xC2738C37, 0xE2E3, 0x4686, 0xB1, 0x6B, 0x30, 0x7A, 0x68, 0x3B, 0xA1, 0xA6), "Pulse" },
		{ new(0xA0E2051C, 0xAB8B, 0x4D8C, 0xA9, 0x50, 0xFC, 0x01, 0xBA, 0x6C, 0x31, 0x2A), "Pulse" }, // 5 Colors
		{ new(0xD575547B, 0xF8B0, 0x4F80, 0x98, 0xDF, 0xE1, 0xA9, 0x11, 0x2C, 0x72, 0x70), "Pulse" }, // 8 Colors
		{ new(0x433FC57B, 0x6486, 0x48EA, 0x8F, 0xA1, 0x1D, 0x2A, 0x93, 0xE1, 0x92, 0xCB), "Pulse (Speed)" },
		{ new(0x02531B0C, 0xF13D, 0x4F0F, 0xAC, 0xFD, 0xC3, 0xF0, 0x88, 0x71, 0xA2, 0x7F), "Pulse (Advanced)" },
		{ new(0xA3F80010, 0x5663, 0x4ECF, 0x9C, 0x22, 0x03, 0x6E, 0x28, 0x47, 0x8B, 0x7E), "Flash" },
		{ new(0x2FC31DBB, 0x1974, 0x4859, 0x90, 0xA3, 0xCE, 0x11, 0xC9, 0x24, 0x4B, 0xB1), "Flash" }, // 5 Colors
		{ new(0x1D3B40F9, 0xAB2A, 0x43F4, 0x8A, 0x81, 0xC4, 0xBC, 0x90, 0x33, 0xB4, 0xA5), "Flash" }, // 8 Colors
		{ new(0xF786DD5A, 0x83A7, 0x4B07, 0x91, 0x66, 0xD2, 0x31, 0xEF, 0x2D, 0xDE, 0x11), "Flash (Speed)" },
		{ new(0x99D1CFC4, 0xB25D, 0x4EEF, 0xAB, 0x17, 0x05, 0x2D, 0xF3, 0xD4, 0x93, 0x2D), "Flash (Advanced)" },
		{ new(0x2C497719, 0x8477, 0x4FE2, 0x80, 0x45, 0x48, 0x89, 0x39, 0xD4, 0xC9, 0x13), "Double Flash" },
		{ new(0x08B2116E, 0xCD5C, 0x4990, 0xA8, 0x73, 0x09, 0x14, 0xD6, 0x39, 0x17, 0x37), "Double Flash (Speed)" },
		{ new(0x9FD48D3C, 0x9BD5, 0x403E, 0x8F, 0xFD, 0x8F, 0xF6, 0x6F, 0x47, 0xB2, 0x32), "Chase" },
		{ new(0xBAB1B0F1, 0x55F3, 0x46F8, 0x9A, 0xDC, 0xE6, 0x98, 0x4B, 0x38, 0x19, 0x02), "Chase" }, // Aura RAM
		{ new(0xA4FBC975, 0x3CBF, 0x48AA, 0x9B, 0xFB, 0x1F, 0x12, 0x89, 0xE7, 0xC3, 0xA0), "Chase (Speed)" },
		{ new(0xF64133DF, 0x043A, 0x4E9F, 0x82, 0xCA, 0x64, 0x89, 0xB8, 0xF7, 0x86, 0xA7), "Wave" },
		{ new(0x3798FD63, 0x6B69, 0x4167, 0xAD, 0x09, 0xB0, 0x06, 0x20, 0x82, 0x17, 0x8C), "Wave (Speed)" },
		{ new(0x2CB30144, 0x2586, 0x4780, 0x95, 0x6C, 0x43, 0x19, 0x8A, 0xF2, 0x72, 0x6F), "Wave" }, // Aura RAM
		{ new(0x2818D561, 0x15FB, 0x43B0, 0x9C, 0x2E, 0x9F, 0xF5, 0x08, 0x82, 0x2B, 0x7A), "Spectrum Cycle" },
		{ new(0x712094B5, 0xC5B9, 0x4A2B, 0x96, 0x19, 0xE3, 0x3F, 0xB0, 0x49, 0xEE, 0x9F), "Spectrum Cycle (Speed)" },
		{ new(0x7CDBCE50, 0x63FA, 0x42A4, 0x92, 0xE1, 0xDE, 0x7D, 0x91, 0x52, 0x1F, 0x4D), "Spectrum Cycle (Brightness)" },
		{ new(0xB93254E0, 0xD39C, 0x40DF, 0xBF, 0x1F, 0x89, 0xD6, 0xCE, 0xB6, 0x16, 0x15), "Spectrum Wave" },
		{ new(0xD11B8022, 0x2C92, 0x467A, 0xB8, 0x63, 0x9B, 0x70, 0x3D, 0x26, 0x5A, 0x70), "Spectrum Wave (Speed)" },
		{ new(0xF6A8C369, 0xD230, 0x4E63, 0xB6, 0x00, 0xA4, 0x4F, 0x1B, 0x3B, 0xBE, 0xCA), "Spectrum Wave" }, // Aura RAM
		{ new(0x716E4BB3, 0x6725, 0x4D98, 0x86, 0x3B, 0xE8, 0xDD, 0xE8, 0xA7, 0x87, 0xB6), "Spectrum Cycle Pulse" },
		{ new(0xB815E8FD, 0x7AD5, 0x4A48, 0x8F, 0xDB, 0x04, 0x43, 0x1F, 0x15, 0x98, 0x53), "Spectrum Cycle Chase" },
		{ new(0x17325DE9, 0x9572, 0x41C7, 0xA1, 0xE7, 0x8D, 0x1B, 0x2A, 0x28, 0xEA, 0x30), "Wide Spectrum Cycle Chase" },
		{ new(0x36DE8ED5, 0x1783, 0x4EFC, 0xB3, 0x1A, 0xA9, 0xB0, 0xB6, 0x56, 0xB0, 0x9A), "Spectrum Cycle Wave" },
		{ new(0xEDD773B0, 0x727E, 0x4C12, 0xB9, 0x2A, 0xDA, 0x05, 0x5A, 0xCE, 0x49, 0x91), "Alternate Spectrums" },
		{ new(0x1FA781E9, 0x2426, 0x4F06, 0x9B, 0x6E, 0x72, 0x55, 0xEE, 0x02, 0xA4, 0x3A), "Sparkling Spectrum Cycle" },

		{ new(0xA175E0AD, 0xF649, 0x4F10, 0x99, 0xE2, 0xC3, 0xC9, 0x4D, 0x1C, 0x9B, 0xC7), "Reactive" },

		{ new(0xB7CE4E5E, 0x4983, 0x4D3B, 0xA0, 0xD1, 0xEE, 0xB2, 0x28, 0x28, 0x37, 0x76), "Random Color Pulses" },
		{ new(0x45E154D6, 0x1946, 0x4215, 0xA4, 0x4F, 0x97, 0x5C, 0xB7, 0x6D, 0xEE, 0xAE), "Two Color Pulses" },

		{ new(0x85397AFD, 0xFFC7, 0x4C0C, 0x91, 0x45, 0x6D, 0xF9, 0x1D, 0x55, 0x68, 0x66), "Static Color 1" },
		{ new(0x52F39EE8, 0xB4CD, 0x492B, 0xB9, 0x9B, 0xF4, 0x9A, 0xBB, 0x4C, 0xFB, 0x71), "Static Color 2" },
		{ new(0x511BA640, 0x295B, 0x425D, 0xB1, 0x8A, 0xEB, 0xED, 0x49, 0xC0, 0xA6, 0x48), "Static Color 3" },
		{ new(0x2959B1E6, 0xC0D4, 0x4D5B, 0xA8, 0x0B, 0xC0, 0x6D, 0x7E, 0x2C, 0x4B, 0x74), "Static Color 4" },

	};

	public static string? GetEffectDisplayName(Guid effectId)
		=> EffectNames.TryGetValue(effectId, out string? name) ? name : null;
}

internal static class SensorDatabase
{
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> SensorNames = new()
	{
		{ new(0xD8D74A16, 0x020B, 0x4ADD, 0xB8, 0x61, 0x7B, 0x64, 0x04, 0x37, 0x58, 0x65), "Temperature (Other)" },
		{ new(0xAE5A078C, 0xE473, 0x4D0D, 0x81, 0x38, 0xCA, 0x7D, 0xD8, 0x85, 0x38, 0x4F), "Temperature" },

		{ new(0x18156D37, 0x73C7, 0x4388, 0x9A, 0x13, 0xC5, 0x9D, 0x78, 0x8C, 0x0B, 0x33), "Fan Speed" },

		{ new(0xD68E3B11, 0xD6EF, 0x44BF, 0x81, 0x46, 0x01, 0x99, 0x3A, 0x93, 0x9B, 0x08), "Input Voltage" },
		{ new(0x5A094A6B, 0xF036, 0x4BF8, 0x92, 0x58, 0x5B, 0xA7, 0x14, 0x62, 0x7C, 0x19), "Output Power" },

		{ new(0xB9B10BBF, 0x2BF1, 0x424A, 0x8F, 0x03, 0x99, 0x32, 0xD3, 0x34, 0x60, 0x64), "12V Voltage" },
		{ new(0x9B6D76E4, 0xF770, 0x40AB, 0x88, 0x53, 0x6B, 0x15, 0xB9, 0xE8, 0xFE, 0xF7), "12V Current" },
		{ new(0x590FD05F, 0x29E6, 0x4E57, 0x82, 0xD3, 0xC9, 0x3A, 0x59, 0xB9, 0xCE, 0xB5), "12V Power" },

		{ new(0xB5E2B134, 0x1E8E, 0x464E, 0xAA, 0xA2, 0x04, 0x89, 0xDC, 0x13, 0xCF, 0xF7), "5V Voltage" },
		{ new(0x3FB507D5, 0xF982, 0x4CA6, 0xBD, 0xF8, 0x42, 0x61, 0xF5, 0x02, 0x8F, 0x9C), "5V Current" },
		{ new(0xD7FA6A2B, 0x0296, 0x46B5, 0x93, 0xA3, 0x21, 0xC7, 0xBE, 0x1B, 0x5C, 0x42), "5V Power" },

		{ new(0x7F3EF7D1, 0xA881, 0x43E4, 0x8E, 0xF5, 0xFA, 0x6E, 0x20, 0xF4, 0x91, 0x3D), "3.3V Voltage" },
		{ new(0xDBAB37C2, 0x4D8F, 0x4EE2, 0xA9, 0xDB, 0xB0, 0xDD, 0x48, 0xB0, 0x30, 0xC0), "3.3V Current" },
		{ new(0x582060CE, 0xE985, 0x41F0, 0x95, 0x2B, 0x36, 0x7F, 0xDD, 0x3A, 0x5B, 0x40), "3.3V Power" },

		{ new(0x3428225A, 0x6BE4, 0x44AF, 0xB1, 0xCD, 0x80, 0x0A, 0x55, 0xF9, 0x43, 0x2F), "Fan Speed" },
		{ new(0xFCF6D2E1, 0x9048, 0x4E87, 0xAC, 0x9F, 0x91, 0xE2, 0x50, 0xE1, 0x21, 0x8B), "Fan 1 Speed" },
		{ new(0xBE0F57CB, 0xCD6D, 0x4422, 0xA0, 0x24, 0xB2, 0x8F, 0xF4, 0x25, 0xE9, 0x03), "Fan 2 Speed" },

		{ new(0x005F94DD, 0x09F5, 0x46D3, 0x99, 0x02, 0xE1, 0x5D, 0x6A, 0x19, 0xD8, 0x24), "Graphics" },
		{ new(0xBF9AAD1D, 0xE013, 0x4178, 0x97, 0xB3, 0x42, 0x20, 0xD2, 0x6C, 0xBE, 0x71), "Frame Buffer" },
		{ new(0x147C8F52, 0x1402, 0x4515, 0xB9, 0xFB, 0x41, 0x48, 0xFD, 0x02, 0x12, 0xA4), "Video" },

		{ new(0xB65044BE, 0xA3B0, 0x4EFB, 0x9E, 0xFB, 0x82, 0x6E, 0x3B, 0xD7, 0x60, 0xA0), "GPU Temperature" },
		{ new(0x463292D7, 0x4C7F, 0x4848, 0xBA, 0x9E, 0x71, 0x28, 0x21, 0xDF, 0xD5, 0xE8), "Memory Temperature" },
		{ new(0x73356F82, 0x4194, 0x46DF, 0x9F, 0x9F, 0x25, 0x19, 0x21, 0x01, 0x5F, 0x81), "Power Supply Temperature" },
		{ new(0x359E57BE, 0x577D, 0x46ED, 0xA0, 0x9A, 0xD3, 0xA4, 0x4B, 0x95, 0xCA, 0xE8), "Board Temperature" },

		{ new(0xCD415440, 0xFB17, 0x441D, 0xA6, 0x4F, 0x6C, 0x62, 0x73, 0x34, 0x90, 0x00), "Graphics Frequency" },
		{ new(0xDE435007, 0xCCD1, 0x414E, 0x8C, 0xF1, 0x22, 0x0F, 0xCC, 0xE4, 0xD0, 0x99), "Memory Frequency" },
		{ new(0x6E1311E3, 0xD1FB, 0x4555, 0x8C, 0xD9, 0xD0, 0xF3, 0xF6, 0x43, 0x31, 0xEB), "Processor Frequency" },
		{ new(0xD9272343, 0x5AC4, 0x4CD8, 0x9F, 0x7A, 0x02, 0xE1, 0x30, 0xC1, 0x5F, 0xD3), "Video Frequency" },

		{ new(0xD9CA6694, 0x7514, 0x429C, 0x86, 0x53, 0x66, 0x55, 0xC4, 0x30, 0x73, 0xB2), "Power Load" },
		{ new(0xD8C1E0F2, 0x1712, 0x4709, 0x8B, 0x81, 0x3C, 0x2D, 0x2F, 0x77, 0xD6, 0x34), "Output Voltage" },

		{ new(0x8E880DE1, 0x2A45, 0x400D, 0xA9, 0x0F, 0x42, 0xE8, 0x9B, 0xF9, 0x50, 0xDB), "Liquid Temperature" },
		{ new(0x3A2F0F14, 0x3957, 0x400E, 0x8B, 0x6C, 0xCB, 0x02, 0x5B, 0x89, 0x15, 0x06), "Pump Speed" },
		{ new(0xFDC93D5B, 0xEDE3, 0x4774, 0x96, 0xEC, 0xC4, 0xFD, 0xB1, 0xC1, 0xDE, 0xBC), "Fan Speed" },
	};

	public static string? GetSensorDisplayName(Guid sensorId)
		=> SensorNames.TryGetValue(sensorId, out string? name) ? name : null;
}

internal static class CoolerDatabase
{
	// TODO: Migrate to external files.
	private static readonly Dictionary<Guid, string> CoolerNames = new()
	{
		{ new(0x4AE632C6, 0x5A28, 0x4722, 0xAE, 0xBE, 0x7D, 0x8F, 0x23, 0xE9, 0xF4, 0x2D), "Fan" },

		{ new(0x2A57C838, 0xCD58, 0x4D6C, 0xAF, 0x9E, 0xF5, 0xBD, 0xDD, 0x6F, 0xB9, 0x92), "Pump" },
		{ new(0x5A0FE6F5, 0xB7D1, 0x46E4, 0xA5, 0x12, 0x82, 0x72, 0x6E, 0x95, 0x35, 0xC4), "Fan" },
	};

	public static string? GetCoolerDisplayName(Guid sensorId)
		=> CoolerNames.TryGetValue(sensorId, out string? name) ? name : null;
}
