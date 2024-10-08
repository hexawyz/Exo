namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidConsumerUsage : ushort
	{
		Undefined = 0x00,
		ConsumerControl = 0x01,
		NumericKeyPad = 0x02,
		ProgrammableButtons = 0x03,
		Microphone = 0x04,
		Headphone = 0x05,
		GraphicEqualizer = 0x06,

		Plus10 = 0x20,
		Plus100 = 0x21,
		AmPm = 0x22,

		Power = 0x30,
		Reset = 0x31,
		Sleep = 0x32,
		SleepAfter = 0x33,
		SleepMode = 0x34,
		Illumination = 0x35,
		FunctionButtons = 0x36,

		Menu = 0x40,
		MenuPick = 0x41,
		MenuUp = 0x42,
		MenuDown = 0x43,
		MenuLeft = 0x44,
		MenuRight = 0x45,
		MenuEscape = 0x46,
		MenuValueIncrease = 0x47,
		MenuValueDecrease = 0x48,

		DataOnScreen = 0x60,
		ClosedCaption = 0x61,
		ClosedCaptionSelect = 0x62,
		VcrTv = 0x63,
		BroadcastMode = 0x64,
		Snapshot = 0x65,
		Still = 0x66,

		PictureInPictureToggle = 0x67,
		PictureInPictureSwap = 0x68,
		RedMenuButton = 0x69,
		GreenMenuButton = 0x6A,
		BlueMenuButton = 0x6B,
		YellowMenuButton = 0x6C,
		Aspect = 0x6D,
		ModeSelect3d = 0x6E,
		DisplayBrightnessIncrement = 0x6F,
		DisplayBrightnessDecrement = 0x70,
		DisplayBrightness = 0x71,
		DisplayBacklightToggle = 0x72,
		DisplaySetBrightnessToMinimum = 0x73,
		DisplaySetBrightnessToMaximum = 0x74,
		DisplaySetAutoBrightness = 0x75,

		CameraAccessEnabled = 0x76,
		CameraAccessDisabled = 0x77,
		CameraAccessToggle = 0x78,

		KeyboardBrightnessIncrement = 0x79,
		KeyboardBrightnessDecrement = 0x7A,
		KeyboardBacklightSetLevel = 0x7B,
		KeyboardBacklightOoc = 0x7C,
		KeyboardBacklightSetMinimum = 0x7D,
		KeyboardBacklightSetMaximum = 0x7E,
		KeyboardBacklightAuto = 0x7F,

		Selection = 0x80,
		AssignSelection = 0x81,
		ModeStep = 0x82,
		RecallLast = 0x83,
		EnterChannel = 0x84,
		OrderMovie = 0x85,
		Channel = 0x86,

		MediaSelection = 0x87,
		MediaSelectComputer = 0x88,
		MediaSelectTv = 0x89,
		MediaSelectWww = 0x8A,
		MediaSelectDvd = 0x8B,
		MediaSelectTelephone = 0x8C,
		MediaSelectProgramGuide = 0x8D,
		MediaSelectVideoPhone = 0x8E,
		MediaSelectGames = 0x8F,
		MediaSelectMessages = 0x90,
		MediaSelectCd = 0x91,
		MediaSelectVcr = 0x92,
		MediaSelectTuner = 0x93,
		Quit = 0x94,
		Help = 0x95,
		MediaSelectTape = 0x96,
		MediaSelectCable = 0x97,
		MediaSelectSatellite = 0x98,
		MediaSelectSecurity = 0x99,
		MediaSelectHome = 0x9A,
		MediaSelectCall = 0x9B,
		ChannelIncrement = 0x9C,
		ChannelDecrement = 0x9D,
		MediaSelectSap = 0x9E,
		VcrPlus = 0xA0,
		Once = 0xA1,
		Daily = 0xA2,
		Weekly = 0xA3,
		Monthly = 0xA4,
		Play = 0xB0,
		Pause = 0xB1,
		Record = 0xB2,
		FastForward = 0xB3,
		Rewind = 0xB4,
		ScanNextTrack = 0xB5,
		ScanPreviousTrack = 0xB6,
		Stop = 0xB7,
		Eject = 0xB8,
		RandomPlay = 0xB9,
		SelectDisc = 0xBA,
		EnterDisc = 0xBB,
		Repeat = 0xBC,
		Tracking = 0xBD,
		TrackNormal = 0xBE,
		SlowTracking = 0xBF,
		FrameForward = 0xC0,
		FrameBack = 0xC1,
		Mark = 0xC2,
		ClearMark = 0xC3,
		RepeatFromMark = 0xC4,
		ReturnToMark = 0xC5,
		SearchMarkForward = 0xC6,
		SearchMarkBackwards = 0xC7,
		CounterReset = 0xC8,
		ShowCounter = 0xC9,
		TrackingIncrement = 0xCA,
		TrackingDecrement = 0xCB,
		StopEject = 0xCC,
		PlayPause = 0xCD,
		PlaySkip = 0xCE,
		VoiceCommand = 0xCF,

		InvokeCaptureInterface = 0xD0,
		StartOrStopGameRecording = 0xD1,
		HistoricalGameCapture = 0xD2,
		CaptureGameScreenshot = 0xD3,
		ShowOrHideRecordingIndicator = 0xD4,
		StartOrStopMicrophoneCapture = 0xD5,
		StartOrStopCameraCapture = 0xD6,
		StartOrStopGameBroadcast = 0xD7,
		StartOrStopVoiceDictationSession = 0xD8,

		InvokeOrDismissEmojiPicker = 0xD9,

		Volume = 0xE0,
		Balance = 0xE1,
		Mute = 0xE2,
		Bass = 0xE3,
		Treble = 0xE4,
		BassBoost = 0xE5,
		SurroundMode = 0xE6,
		Loudness = 0xE7,
		Multiplexer = 0xE8,
		VolumeIncrement = 0xE9,
		VolumeDecrement = 0xEA,

		SpeedSelect = 0xF0,
		PlaybackSpeed = 0xF1,
		StandardPlay = 0xF2,
		LongPlay = 0xF3,
		ExtendedPlay = 0xF4,
		Slow = 0xF5,

		FanEnable = 0x100,
		FanSpeed = 0x101,
		LightEnable = 0x102,
		LightIlluminationLevel = 0x103,
		ClimateControlEnable = 0x104,
		RoomTemperature = 0x105,
		SecurityEnable = 0x106,
		FireAlarm = 0x107,
		PoliceAlarm = 0x108,
		Proximity = 0x109,
		Motion = 0x10A,
		DuressAlarm = 0x10B,
		HoldupAlarm = 0x10C,
		MedicalAlarm = 0x10D,

		BalanceRight = 0x150,
		BalanceLeft = 0x151,
		BassIncrement = 0x152,
		BassDecrement = 0x153,
		TrebleIncrement = 0x154,
		TrebleDecrement = 0x155,

		SpeakerSystem = 0x160,
		ChannelLeft = 0x161,
		ChannelRight = 0x162,
		ChannelCenter = 0x163,
		ChannelFront = 0x164,
		ChannelCenterFront = 0x165,
		ChannelSide = 0x166,
		ChannelSurround = 0x167,
		ChannelLowFrequencyEnhancement = 0x168,
		ChannelTop = 0x169,
		ChannelUnknown = 0x16A,

		SubChannel = 0x170,
		SubChannelIncrement = 0x171,
		SubChannelDecrement = 0x172,
		AlternateAudioIncrement = 0x173,
		AlternateAudioDecrement = 0x174,

		ApplicationLaunchButtons = 0x180,
		ApplicationLaunchLaunchButtonConfigurationTool = 0x181,
		ApplicationLaunchProgrammableButtonConfiguration = 0x182,
		ApplicationLaunchConsumerControlConfiguration = 0x183,
		ApplicationLaunchWordProcessor = 0x184,
		ApplicationLaunchTextEditor = 0x185,
		ApplicationLaunchSpreadsheet = 0x186,
		ApplicationLaunchGraphicsEditor = 0x187,
		ApplicationLaunchPresentationApp = 0x188,
		ApplicationLaunchDatabaseApp = 0x189,
		ApplicationLaunchEmailReader = 0x18A,
		ApplicationLaunchNewsreader = 0x18B,
		ApplicationLaunchVoicemail = 0x18C,
		ApplicationLaunchContactsAddressBook = 0x18D,
		ApplicationLaunchCalendarSchedule = 0x18E,
		ApplicationLaunchTaskProjectManager = 0x18F,
		ApplicationLaunchLogJournalTimecard = 0x190,
		ApplicationLaunchCheckbookFinance = 0x191,
		ApplicationLaunchCalculator = 0x192,
		ApplicationLaunchAvCapturePlayback = 0x193,
		ApplicationLaunchLocalMachineBrowser = 0x194,
		ApplicationLaunchLanWanBrowser = 0x195,
		ApplicationLaunchInternetBrowser = 0x196,
		ApplicationLaunchRemoteNetworkingIspConnect = 0x197,
		ApplicationLaunchNetworkConference = 0x198,
		ApplicationLaunchNetworkChat = 0x199,
		ApplicationLaunchTelephonyDialer = 0x19A,
		ApplicationLaunchLogon = 0x19B,
		ApplicationLaunchLogoff = 0x19C,
		ApplicationLaunchLogonLogoff = 0x19D,
		ApplicationLaunchTerminalLockScreensaver = 0x19E,
		ApplicationLaunchControlPanel = 0x19F,
		ApplicationLaunchCommandLineProcessorRun = 0x1A0,
		ApplicationLaunchProcessTaskManager = 0x1A1,
		ApplicationLaunchSelectTaskApplication = 0x1A2,
		ApplicationLaunchNextTaskApplication = 0x1A3,
		ApplicationLaunchPreviousTaskApplication = 0x1A4,
		ApplicationLaunchPreemptiveHaltTaskApplication = 0x1A5,
		ApplicationLaunchIntegratedHelpCenter = 0x1A6,
		ApplicationLaunchDocuments = 0x1A7,
		ApplicationLaunchThesaurus = 0x1A8,
		ApplicationLaunchDictionary = 0x1A9,
		ApplicationLaunchDesktop = 0x1AA,
		ApplicationLaunchSpellCheck = 0x1AB,
		ApplicationLaunchGrammarCheck = 0x1AC,
		ApplicationLaunchWirelessStatus = 0x1AD,
		ApplicationLaunchKeyboardLayout = 0x1AE,
		ApplicationLaunchVirusProtection = 0x1AF,
		ApplicationLaunchEncryption = 0x1B0,
		ApplicationLaunchScreenSaver = 0x1B1,
		ApplicationLaunchAlarms = 0x1B2,
		ApplicationLaunchClock = 0x1B3,
		ApplicationLaunchFileBrowser = 0x1B4,
		ApplicationLaunchPowerStatus = 0x1B5,
		ApplicationLaunchImageBrowser = 0x1B6,
		ApplicationLaunchAudioBrowser = 0x1B7,
		ApplicationLaunchMovieBrowser = 0x1B8,
		ApplicationLaunchDigitalRightsManager = 0x1B9,
		ApplicationLaunchDigitalWallet = 0x1BA,

		ApplicationLaunchInstantMessaging = 0x1BC,
		ApplicationLaunchOemFeaturesTipsTutorialBrowser = 0x1BD,
		ApplicationLaunchOemHelp = 0x1BE,
		ApplicationLaunchOnlineCommunity = 0x1BF,
		ApplicationLaunchEntertainmentContentBrowser = 0x1C0,
		ApplicationLaunchOnlineShoppingBrowser = 0x1C1,
		ApplicationLaunchSmartCardInformationHelp = 0x1C2,
		ApplicationLaunchMarketMonitorFinanceBrowser = 0x1C3,
		ApplicationLaunchCustomizedCorporateNewsBrowser = 0x1C4,
		ApplicationLaunchOnlineActivityBrowser = 0x1C5,
		ApplicationLaunchResearchSearchBrowser = 0x1C6,
		ApplicationLaunchAudioPlayer = 0x1C7,
		ApplicationLaunchMessageStatus = 0x1C8,
		ApplicationLaunchContactSync = 0x1C9,
		ApplicationLaunchNavigation = 0x1CA,
		ApplicationLaunchContextAwareDesktopAssistant = 0x1CB,

		GenericGuiApplicationControls = 0x200,
		ApplicationControlNew = 0x201,
		ApplicationControlOpen = 0x202,
		ApplicationControlClose = 0x203,
		ApplicationControlExit = 0x204,
		ApplicationControlMaximize = 0x205,
		ApplicationControlMinimize = 0x206,
		ApplicationControlSave = 0x207,
		ApplicationControlPrint = 0x208,
		ApplicationControlProperties = 0x209,

		ApplicationControlUndo = 0x21A,
		ApplicationControlCopy = 0x21B,
		ApplicationControlCut = 0x21C,
		ApplicationControlPaste = 0x21D,
		ApplicationControlSelectAll = 0x21E,
		ApplicationControlFind = 0x21F,
		ApplicationControlFindAndReplace = 0x220,
		ApplicationControlSearch = 0x221,
		ApplicationControlGoTo = 0x222,
		ApplicationControlHome = 0x223,
		ApplicationControlBack = 0x224,
		ApplicationControlForward = 0x225,
		ApplicationControlStop = 0x226,
		ApplicationControlRefresh = 0x227,
		ApplicationControlPreviousLink = 0x228,
		ApplicationControlNextLink = 0x229,
		ApplicationControlBookmarks = 0x22A,
		ApplicationControlHistory = 0x22B,
		ApplicationControlSubscriptions = 0x22C,
		ApplicationControlZoomIn = 0x22D,
		ApplicationControlZoomOut = 0x22E,
		ApplicationControlZoom = 0x22F,
		ApplicationControlFullScreenView = 0x230,
		ApplicationControlNormalView = 0x231,
		ApplicationControlViewToggle = 0x232,
		ApplicationControlScrollUp = 0x233,
		ApplicationControlScrollDown = 0x234,
		ApplicationControlScroll = 0x235,
		ApplicationControlPanLeft = 0x236,
		ApplicationControlPanRight = 0x237,
		ApplicationControlPan = 0x238,
		ApplicationControlNewWindow = 0x239,
		ApplicationControlTileHorizontally = 0x23A,
		ApplicationControlTileVertically = 0x23B,
		ApplicationControlFormat = 0x23C,
		ApplicationControlEdit = 0x23D,
		ApplicationControlBold = 0x23E,
		ApplicationControlItalics = 0x23F,
		ApplicationControlUnderline = 0x240,
		ApplicationControlStrikethrough = 0x241,
		ApplicationControlSubscript = 0x242,
		ApplicationControlSuperscript = 0x243,
		ApplicationControlAllCaps = 0x244,
		ApplicationControlRotate = 0x245,
		ApplicationControlResize = 0x246,
		ApplicationControlFlipHorizontal = 0x247,
		ApplicationControlFlipVertical = 0x248,
		ApplicationControlMirrorHorizontal = 0x249,
		ApplicationControlMirrorVertical = 0x24A,
		ApplicationControlFontSelect = 0x24B,
		ApplicationControlFontColor = 0x24C,
		ApplicationControlFontSize = 0x24D,
		ApplicationControlJustifyLeft = 0x24E,
		ApplicationControlJustifyCenterH = 0x24F,
		ApplicationControlJustifyRight = 0x250,
		ApplicationControlJustifyBlockH = 0x251,
		ApplicationControlJustifyTop = 0x252,
		ApplicationControlJustifyCenterV = 0x253,
		ApplicationControlJustifyBottom = 0x254,
		ApplicationControlJustifyBlockV = 0x255,
		ApplicationControlIndentDecrease = 0x256,
		ApplicationControlIndentIncrease = 0x257,
		ApplicationControlNumberedList = 0x258,
		ApplicationControlRestartNumbering = 0x259,
		ApplicationControlBulletedList = 0x25A,
		ApplicationControlPromote = 0x25B,
		ApplicationControlDemote = 0x25C,
		ApplicationControlYes = 0x25D,
		ApplicationControlNo = 0x25E,
		ApplicationControlCancel = 0x25F,
		ApplicationControlCatalog = 0x260,
		ApplicationControlBuyCheckout = 0x261,
		ApplicationControlAddToCart = 0x262,
		ApplicationControlExpand = 0x263,
		ApplicationControlExpandAll = 0x264,
		ApplicationControlCollapse = 0x265,
		ApplicationControlCollapseAll = 0x266,
		ApplicationControlPrintPreview = 0x267,
		ApplicationControlPasteSpecial = 0x268,
		ApplicationControlInsertMode = 0x269,
		ApplicationControlDelete = 0x26A,
		ApplicationControlLock = 0x26B,
		ApplicationControlUnlock = 0x26C,
		ApplicationControlProtect = 0x26D,
		ApplicationControlUnprotect = 0x26E,
		ApplicationControlAttachComment = 0x26F,
		ApplicationControlDeleteComment = 0x270,
		ApplicationControlViewComment = 0x271,
		ApplicationControlSelectWord = 0x272,
		ApplicationControlSelectSentence = 0x273,
		ApplicationControlSelectParagraph = 0x274,
		ApplicationControlSelectColumn = 0x275,
		ApplicationControlSelectRow = 0x276,
		ApplicationControlSelectTable = 0x277,
		ApplicationControlSelectObject = 0x278,
		ApplicationControlRedoRepeat = 0x279,
		ApplicationControlSort = 0x27A,
		ApplicationControlSortAscending = 0x27B,
		ApplicationControlSortDescending = 0x27C,
		ApplicationControlFilter = 0x27D,
		ApplicationControlSetClock = 0x27E,
		ApplicationControlViewClock = 0x27F,
		ApplicationControlSelectTimeZone = 0x280,
		ApplicationControlEditTimeZones = 0x281,
		ApplicationControlSetAlarm = 0x282,
		ApplicationControlClearAlarm = 0x283,
		ApplicationControlSnoozeAlarm = 0x284,
		ApplicationControlResetAlarm = 0x285,
		ApplicationControlSynchronize = 0x286,
		ApplicationControlSendReceive = 0x287,
		ApplicationControlSendTo = 0x288,
		ApplicationControlReply = 0x289,
		ApplicationControlReplyAll = 0x28A,
		ApplicationControlForwardMsg = 0x28B,
		ApplicationControlSend = 0x28C,
		ApplicationControlAttachFile = 0x28D,
		ApplicationControlUpload = 0x28E,
		ApplicationControlDownload = 0x28F,
		ApplicationControlSetBorders = 0x290,
		ApplicationControlInsertRow = 0x291,
		ApplicationControlInsertColumn = 0x292,
		ApplicationControlInsertFile = 0x293,
		ApplicationControlInsertPicture = 0x294,
		ApplicationControlInsertObject = 0x295,
		ApplicationControlInsertSymbol = 0x296,
		ApplicationControlSaveAndClose = 0x297,
		ApplicationControlRename = 0x298,
		ApplicationControlMerge = 0x299,
		ApplicationControlSplit = 0x29A,
		ApplicationControlDistributeHorizontally = 0x29B,
		ApplicationControlDistributeVertically = 0x29C,

		ApplicationControlNextKeyboardLayoutSelect = 0x29D,
		ApplicationControlNavigationGuidance = 0x29E,
		ApplicationControlDesktopShowAllWindows = 0x29F,
		ApplicationControlSoftKeyLeft = 0x2A0,
		ApplicationControlSoftKeyRight = 0x2A1,
		ApplicationControlDesktopShowAllApplications = 0x2A2,

		ApplicationControlIdleKeepAlive = 0x2B0,

		ExtendedKeyboardAttributesCollection = 0x2C0,
		KeyboardFormFactor = 0x2C1,
		KeyboardKeyType = 0x2C2,
		KeyboardPhysicalLayout = 0x2C3,
		VendorSpecificKeyboardPhysicalLayout = 0x2C4,
		KeyboardIetfLanguageTagIndex = 0x2C5,
		ImplementedKeyboardInputAssistControls = 0x2C6,
		KeyboardInputAssistPrevious = 0x2C7,
		KeyboardInputAssistNext = 0x2C8,
		KeyboardInputAssistPreviousGroup = 0x2C9,
		KeyboardInputAssistNextGroup = 0x2CA,
		KeyboardInputAssistAccept = 0x2CB,
		KeyboardInputAssistCancel = 0x2CC,

		PrivacyScreenToggle = 0x2D0,
		PrivacyScreenLevelDecrement = 0x2D1,
		PrivacyScreenLevelIncrement = 0x2D2,
		PrivacyScreenLevelMinimum = 0x2D3,
		PrivacyScreenLevelMaximum = 0x2D4,

		ContactEdited = 0x500,
		ContactAdded = 0x501,
		ContactRecordActive = 0x502,
		ContactIndex = 0x503,
		ContactNickname = 0x504,
		ContactFirstName = 0x505,
		ContactLastName = 0x506,
		ContactFullName = 0x507,
		ContactPhoneNumberPersonal = 0x508,
		ContactPhoneNumberBusiness = 0x509,
		ContactPhoneNumberMobile = 0x50A,
		ContactPhoneNumberPager = 0x50B,
		ContactPhoneNumberFax = 0x50C,
		ContactPhoneNumberOther = 0x50D,
		ContactEmailPersonal = 0x50E,
		ContactEmailBusiness = 0x50F,
		ContactEmailOther = 0x510,
		ContactEmailMain = 0x511,
		ContactSpeedDialNumber = 0x512,
		ContactStatusFlag = 0x513,
		ContactMisc = 0x154,
	}
}
