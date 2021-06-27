namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidKeyboardUsage : ushort
	{
		// Error "keys"
		NoEvent = 0x00,
		Rollover = 0x01,
		Postfail = 0x02,
		Undefined = 0x03,

		// Letters
		A = 0x04,
		B = 0x05,
		C = 0x06,
		D = 0x07,
		E = 0x08,
		F = 0x09,
		G = 0x0A,
		H = 0x0B,
		I = 0x0C,
		J = 0x0D,
		K = 0x0E,
		L = 0x0F,
		M = 0x10,
		N = 0x11,
		O = 0x12,
		P = 0x13,
		Q = 0x14,
		R = 0x15,
		S = 0x16,
		T = 0x17,
		U = 0x18,
		V = 0x19,
		W = 0x1A,
		X = 0x1B,
		Y = 0x1C,
		Z = 0x1D,

		// Numbers
		One = 0x1E,
		Two = 0x1F,
		Three = 0x20,
		Four = 0x21,
		Five = 0x22,
		Six = 0x23,
		Seven = 0x24,
		Eight = 0x25,
		Nine = 0x26,
		Zero = 0x27,

		Return = 0x28,
		Escape = 0x29,
		Delete = 0x2A,
		Tab = 0x2B,
		Spacebar = 0x2C,

		/// <summary>Keyboard - and _ (US)</summary>
		OemMinus = 0x2D,
		/// <summary>Keyboard = and + (US)</summary>
		OemPlus = 0x2E,
		/// <summary>Keyboard [ and { (US)</summary>
		Oem4 = 0x2F,
		/// <summary>Keyboard ] and } (US)</summary>
		Oem6 = 0x30,
		/// <summary>Keyboard \ and | (US)</summary>
		Oem5 = 0x31,
		/// <summary>Keyboard # and ~ (non-US)</summary>
		XXXXXXXXXXXXXXXX = 0x32,
		/// <summary>Keyboard ; and : (US)</summary>
		Oem1 = 0x33,
		/// <summary>Keyboard ' and " (US)</summary>
		Oem7 = 0x34,
		/// <summary>Keyboard ` and ~ (US)</summary>
		Oem3 = 0x35,
		/// <summary>Keyboard , and < (US)</summary>
		Comma = 0x36,
		/// <summary>Keyboard . and > (US)</summary>
		Period = 0x37,
		/// <summary>Keyboard / and ? (US)</summary>
		Oem2 = 0x38,

		CapsLock = 0x39,

		// Standard Function Keys
		F1 = 0x3A,
		F2 = 0x3B,
		F3 = 0x3C,
		F4 = 0x3D,
		F5 = 0x3E,
		F6 = 0x3F,
		F7 = 0x40,
		F8 = 0x41,
		F9 = 0x42,
		F10 = 0x43,
		F11 = 0x44,
		F12 = 0x45,

		PrintScreen = 0x46,
		ScrollLock = 0x47,
		Pause = 0x48,
		Insert = 0x49,
		Home = 0x4A,
		PageUp = 0x4B,
		DeleteForward = 0x4C,
		End = 0x4D,
		PageDown = 0x4E,
		RightArrow = 0x4F,
		LeftArrow = 0x50,
		DownArrow = 0x51,
		UpArrow = 0x52,

		/// <summary>Keypad Num Lock and Clear</summary>
		NumLock = 0x53,

		/// <summary>Keypad /</summary>
		Divide = 0x54,
		/// <summary>Keypad *</summary>
		Multiply = 0x55,
		/// <summary>Keypad -</summary>
		Subtract = 0x56,
		/// <summary>Keypad +</summary>
		Add = 0x57,
		/// <summary>Keypad ENTER</summary>
		Enter = 0x58,

		/// <summary>Keypad 1 and End</summary>
		Numpad1 = 0x59,
		/// <summary>Keypad 2 and Down Arrow</summary>
		Numpad2 = 0x5A,
		/// <summary>Keypad 3 and PageDn</summary>
		Numpad3 = 0x5B,
		/// <summary>Keypad 4 and Left Arrow</summary>
		Numpad4 = 0x5C,
		/// <summary>Keypad 5</summary>
		Numpad5 = 0x5D,
		/// <summary>Keypad 6 and Right Arrow</summary>
		Numpad6 = 0x5E,
		/// <summary>Keypad 7 and Home</summary>
		Numpad7 = 0x5F,
		/// <summary>Keypad 8 and Up Arrow</summary>
		Numpad8 = 0x60,
		/// <summary>Keypad 9 and PageUp</summary>
		Numpad9 = 0x61,
		/// <summary>Keypad 0 and Insert</summary>
		Numpad0 = 0x62,

		/// <summary>Keypad . and Delete</summary>
		Decimal = 0x63,

		/// <summary>Keyboard \ and | (non-US)</summary>
		/// <remarks>Should be @lt; and @gt; on AERTY keyboard.</remarks>
		Oem102 = 0x64,

		Application = 0x65,

		Power = 0x66,

		NumpadEqual = 0x67,

		// Extended Function Keys
		F13 = 0x68,
		F14 = 0x69,
		F15 = 0x6A,
		F16 = 0x6B,
		F17 = 0x6C,
		F18 = 0x6D,
		F19 = 0x6E,
		F20 = 0x6F,
		F21 = 0x70,
		F22 = 0x71,
		F23 = 0x72,
		F24 = 0x73,

		Execute = 0x74,
		Help = 0x75,
		Menu = 0x76,
		Select = 0x77,
		Stop = 0x78,
		Again = 0x79,
		Undo = 0x7A,
		Cut = 0x7B,
		Copy = 0x7C,
		Paste = 0x7D,
		Find = 0x7E,
		Mute = 0x7F,
		VolumeUp = 0x80,
		VolumeDown = 0x81,

		LockingCapsLock = 0x82,
		LockingNumLock = 0x83,
		LockingScrollLock = 0x84,

		// Special keypad keys
		NumpadComma = 0x85, // Brazilian equivalent of numpad .
		NumpadEqualSign = 0x86, // For AS/400 keyboard

		/// <summary>ろ (Ro)</summary>
		International1 = 0x87,
		/// <summary>かたかな/ひらがな/ローマ字 (Hiragana/Katakana/Romaji)</summary>
		International2 = 0x88,
		/// <summary>¥ (Yen)</summary>
		International3 = 0x89,
		/// <summary>変換 (Henkan)</summary>
		International4 = 0x8A,
		/// <summary>無変換 (Muhenkan)</summary>
		International5 = 0x8B,
		/// <summary>PC9800 Keypad ,</summary>
		International6 = 0x8C,
		International7 = 0x8D,
		International8 = 0x8E,
		International9 = 0x8F,

		/// <summary>Hangul/English</summary>
		Lang1 = 0x90,
		/// <summary>Hanja</summary>
		Lang2 = 0x91,
		/// <summary>Katakana</summary>
		Lang3 = 0x92,
		/// <summary>Hiragana</summary>
		Lang4 = 0x93,
		/// <summary>Zenkaku/Hankaku</summary>
		Lang5 = 0x94,
		Lang6 = 0x95,
		Lang7 = 0x96,
		Lang8 = 0x97,
		Lang9 = 0x98,

		AlternateErase = 0x99,

		SysReq = 0x9A,
		Cancel = 0x9B,
		Clear = 0x9C,
		Prior = 0x9D,
		/// <summary>Keyboard Return</summary>
		ReturnOnly = 0x9E, // ? This is not "Return (ENTER)" so I assume it is return only ?
		Separator = 0x9F,
		Out = 0xA0,
		Oper = 0xA1,
		/// <summary>Keyboard CrSel</summary>
		ClearAgain = 0xA2,
		/// <summary>Keyboard CrSel</summary>
		CursorSelect = 0xA3,
		/// <summary>Keyboard ExSel</summary>
		ExtendSelection = 0xA4,

		Numpad00 = 0xB0,
		Numpad000 = 0xB1,
		ThousandsSeparator = 0xB2,
		DecimalSeparator = 0xB3,
		CurrencyUnit = 0xB4,
		CurrencySubUnit = 0xB5,
		NumpadOpenParenthese = 0xB6,
		NumpadCloseParenthese = 0xB7,
		NumpadOpenBrace = 0xB8,
		NumpadCloseBrace = 0xB9,
		NumpadTab = 0xBA,
		NumpadBackspace = 0xBB,
		NumpadA = 0xBC,
		NumpadB = 0xBD,
		NumpadC = 0xBE,
		NumpadD = 0xBF,
		NumpadE = 0xC0,
		NumpadF = 0xC1,
		NumpadXor = 0xC2,
		NumpadXorSign = 0xC3,
		NumpadModuloSign = 0xC4,
		NumpadLesserThan = 0xC5,
		NumpadGreaterThan = 0xC6,
		NumpadBitwiseAndSign = 0xC7,
		NumpadLogicalAndSign = 0xC8,
		NumpadBitwiseOrSign = 0xC9,
		NumpadLogicalOrSign = 0xCA,
		NumpadColon = 0xCB,
		NumpadPound = 0xCC,
		NumpadSpace = 0xCD,
		NumpadAtSign = 0xCE,
		NumpadExclamationMark = 0xCF,
		NumpadMemoryStore = 0xD0,
		NumpadMemoryRecall = 0xD1,
		NumpadMemoryClear = 0xD2,
		NumpadMemoryAdd = 0xD3,
		NumpadMemorySubtract = 0xD4,
		NumpadMemoryMultiply = 0xD5,
		NumpadMemoryDivide = 0xD6,
		NumapdPlusMinus = 0xD7,
		NumpadClear = 0xD8,
		NumpadClearEntry = 0xD9,
		NumpadBinary = 0xDA,
		NumpadOctal = 0xDB,
		NumpadDecimal = 0xDC,
		NumpadHexadecimal = 0xDD,

		// Modifier Keys
		LeftControl = 0xE0,
		LeftShift = 0xE1,
		LeftAlt = 0xE2,
		LeftGui = 0xE3,
		RightControl = 0xE4,
		RightShift = 0xE5,
		RightAlt = 0xE6,
		RightGui = 0xE7,
	}
}
