namespace DeviceTools.Logitech.HidPlusPlus;

public enum TaskId : ushort
{
	ReprogrammableVolumeUp = 0x0001,
	ReprogrammableVolumeDown = 0x0002,
	ReprogrammableMute = 0x0003,
	ReprogrammablePlayPause = 0x0004,
	ReprogrammableNextTrack = 0x0005,
	ReprogrammablePreviousTrack = 0x0006,

	// This is at least the case on MX Keys for Mac.
	LaunchCalculator = 0x000A,
	Eject = 0x000D,

	LeftClick = 0x0038,
	RightClick = 0x0039,
	MiddleClick = 0x003A,
	MouseButton4 = 0x003C,
	MouseButton5 = 0x003D,

	// Control 0xC2 indicates that this will by default send HID Sleep (0xC 0x0030 is actually Power, and 0x1 0x0082 is Caps Lock ?)
	MultiPlatformLock = 0x44,

	// Indicated for control 0xDE (F Lock) in the doc as "Do nothing" and observed on MX Keys for Mac for what should be the fn key lock. (Can be verified later if necessary)
	DoNothing = 0x62,

	// AKA PageDown
	FunctionPlusDown = 0x6F,
	// AKA PageUp
	FunctionPlusUp = 0x70,

	LaunchSpUi = 0x0092,

	/// <summary>Win + P.</summary>
	SwitchPresentation = 0x93,
	/// <summary>Win + Down Arrow.</summary>
	MinimizeWindow = 0x94,
	/// <summary>Win + Up Arrow.</summary>
	MaximizeWindow = 0x95,
	/// <summary>Mission Control on Mac, App Switch on Windows.</summary>
	MultiPlatformAppSwitchOrMissionControl = 0x96,
	/// <summary>Launchpad on Mac, Browser Homepage on Windows.</summary>
	MultiPlatformNavigateHomeOrLaunchpad = 0x97,
	/// <summary>Right Click on Mac, Contextual Menu on Windows.</summary>
	MultiPlatformContextualMenuOrRightClick = 0x98,
	/// <summary>Swipe Back on Mac, Browser Back on Windows.</summary>
	MultiPlatformNavigateBack = 0x99,
	MacSwitchLanguage = 0x9A,
	MacScreenCapture = 0x9B,
	GestureButton = 0x9C,
	SmartShift = 0x9D,
	AppExpose = 0x9E,
	SmartZoom = 0x9F,
	Lookup = 0xA0,
	// IDK why the documentation mentions lenovo. Brightness controls are clearly working as normal.
	/*Lenovo*/MicrophoneToggle = 0xA1,
	/*Lenovo*/WifiToggle = 0xA2,
	/*Lenovo*/BrightnessDown = 0xA3,
	/*Lenovo*/BrightnessUp = 0xA4,
	/*Lenovo*/DisplayOut = 0xA5,
	/*Lenovo*/ViewOpenApps = 0xA6,
	/*Lenovo*/ViewAllOpenApps = 0xA7,
	AppSwitch = 0xA8,
	GestureButtonNavigation = 0xA9,
	FunctionInversionDisplay = 0xAA,
	/// <summary>Command + [ on Mac, Back on Windows.</summary>
	MultiPlatformBack = 0xAB,
	/// <summary>Command + ] on Mac, Forward on Windows.</summary>
	MultiPlatformForward = 0xAC,
	MultiPlatformGestureButton = 0xAD,
	HostSwitchChannel1 = 0xAE,
	HostSwitchChannel2 = 0xAF,
	HostSwitchChannel3 = 0xB0,
	MultiPlatformSearch = 0xB1,
	MultiPlatformHomeOrMissionControl = 0xB2,
	MultiPlatformMenuOrLaunchpad = 0xB3,
	VirtualGestureButton = 0xB4,
	Cursor = 0xB5,
	// Associated control is NextButton
	KeyboardRightArrow = 0xB6,
	// Associated control is NextButtonLongPress
	SoftwareCustomHighlight = 0xB7,
	// Associated control is BackButton
	KeyboardLeftArrow = 0xB8,
	// Not named directly in the documentation, but this is the name of the associated control.
	PresenterBackButtonLongPress = 0xB9,
	MultiPlatformLanguageSwitch = 0xBA,
	SoftwareCustomHighlight2 = 0xBB,
	FastForward = 0xBC,
	FastBackward = 0xBD,
	SwitchHighlighting = 0xBE,
	MissionControlOrTaskView = 0xBF,
	DashboardOrActionCenter = 0xC0,
	BacklightDown = 0xC1,
	BacklightUp = 0xC2,
	/// <summary>Right Click on Mac, Contextual Menu on Windows.</summary>
	ContextualMenuOrRightClick = 0xC3,
	DpiChange = 0xC4,
	NewTab = 0xC5,
	F2 = 0xC6,
	F3 = 0xC7,
	F4 = 0xC8,
	F5 = 0xC9,
	F6 = 0xCA,
	F7 = 0xCB,
	F8 = 0xCC,
	F1 = 0xCD,
	LaserButton = 0xCE,
	LaserButtonLongPress = 0xCF,
	StartPresentation = 0xD0,
	BlankScreen = 0xD1,
	DpiSwitch = 0xD2,
	MultiPlatformHomeOrShowDesktop = 0xD3,
	MultiPlatformAppSwitchOrDashboard = 0xD4,
	MultiPlatformAppSwitch = 0xD5,
	FunctionInversion = 0xD6,
	LeftAndRightClick = 0xD7,
	VoiceDictation = 0xD8,
	EmojiSmilingFaceWithHeartShapedEyes = 0xD9,
	EmojiLoudlyCryingFace = 0xDA,
	EmojiSmiley = 0xDB,
	EmojiSmileyWithTears = 0xDC,
	// Also LedToggle ??
	OpenEmojiPanel = 0xDD,
	MultiPlatformAppSwitchOrLaunchPad = 0xDE,
	SnippingTool = 0xDF,
	GraveAccent = 0xE0,
	StandardTabKey = 0xE1,
	CapsLock = 0xE2,
	LeftShift = 0xE3,
	LeftControl = 0xE4,
	LeftOptionOrStart = 0xE5,
	LeftCommandOrAlt = 0xE6,
	RightCommandOrAlt = 0xE7,
	RightOptionOrStart = 0xE8,
	RightControl = 0xE9,
	RightShift = 0xEA,
	Insert = 0xEB,
	Delete = 0xEC,
	Home = 0xED,
	End = 0xEE,
	PageUp = 0xEF,
	PageDown = 0xF0,
	MuteMicrophone = 0xF1,
	DoNotDisturb = 0xF2,
	Backslash = 0xF3,
	Refresh = 0xF4,
	CloseTab = 0xF5,
	LanguageSwitch = 0xF6,
	// The documentation calls it "Standard alphabetical key", but from the control IDs documentation, it is clear that this covers pretty much all VK codes.
	StandardKey = 0xF7,
	RightOptionOrStart2 = 0xF8,
	LeftOption = 0xF9,
	RightOption = 0xFA,
	LeftCommand = 0xFB,
	RightCommand = 0xFC,
}
