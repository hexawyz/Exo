using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices.Mccs
{
	public enum VcpCode : byte
	{
		[Category("Preset"), Description("VCP Code Page")]
		VcpCodePage = 0x00,
		[Category("Miscellaneous"), Description("Degauss")]
		Degauss = 0x01,
		[Category("Miscellaneous"), Description("New Control Value")]
		NewControlValue = 0x02,
		[Category("Miscellaneous"), Description("Soft Controls")]
		SoftControls = 0x03,
		[Category("Preset"), Description("Restore Factory Defaults")]
		RestoreFactoryDefaults = 0x04,
		[Category("Preset"), Description("Restore Factory Luminance/Contrast Defaults")]
		RestoreFactoryLuminanceContrastDefaults = 0x05,
		[Category("Preset"), Description("Restore Factory Geometry Defaults")]
		RestoreFactoryGeometryDefaults = 0x06,
		[Category("Preset"), Description("Restore Factory Color Defaults")]
		RestoreFactoryColorDefaults = 0x08,
		[Category("Preset"), Description("Restore Factory TV Defaults")]
		RestoreFactoryTvDefaults = 0x0A,
		[Category("Image"), Description("Color Temperature Increment")]
		ColorTemperatureIncrement = 0x0B,
		[Category("Image"), Description("Color Temperature Request")]
		ColorTemperatureRequest = 0x0C,
		[Category("Image"), Description("Clock")]
		Clock = 0x0E,
		[Category("Image"), Description("Luminance")]
		Luminance = 0x10,
		[Category("Image"), Description("Flesh Tone Enhancement")]
		FleshToneEnhancement = 0x11,
		[Category("Image"), Description("Contrast")]
		Contrast = 0x12,
		[Category("Image"), Description("Backlight Control")]
		BacklightControl = 0x13,
		[Category("Image"), Description("Select Color Preset")]
		SelectColorPreset = 0x14,
		[Category("Image"), Description("Video Gain (Drive): Red")]
		VideoGainRed = 0x16,
		[Category("Image"), Description("User Color Vision Compensation")]
		UserColorVisionCompensation = 0x17,
		[Category("Image"), Description("Video Gain (Drive): Green")]
		VideoGainGreen = 0x18,
		[Category("Image"), Description("Video Gain (Drive): Blue")]
		VideoGainBlue = 0x1A,
		[Category("Image"), Description("Focus")]
		Focus = 0x1C,
		[Category("Image"), Description("Auto Setup")]
		AutoSetup = 0x1E,
		[Category("Image"), Description("Auto Color Setup")]
		AutoColorSetup = 0x1F,
		[Category("Geometry"), Description("Horizontal Position (Phase)")]
		HorizontalPosition = 0x20,
		[Category("Geometry"), Description("Horizontal Size")]
		HorizontalSize = 0x22,
		[Category("Geometry"), Description("Horizontal Pincushion")]
		HorizontalPincushion = 0x24,
		[Category("Geometry"), Description("Horizontal Pincushion Balance")]
		HorizontalPincushionBalance = 0x26,
		[Category("Geometry"), Description("Horizontal Convergence R / B")]
		HorizontalConvergenceRedBlue = 0x28,
		[Category("Geometry"), Description("Horizontal Convergence M / G")]
		HorizontalConvergenceMagentaGreen = 0x29,
		[Category("Geometry"), Description("Horizontal Linearity")]
		HorizontalLinearity = 0x2A,
		[Category("Geometry"), Description("Horizontal Linearity Balance")]
		HorizontalLinearityBalance = 0x2C,
		[Category("Image"), Description("Gray Scale Expansion")]
		GrayScaleExpansion = 0x2E,
		[Category("Geometry"), Description("Vertical Position (Phase)")]
		VerticalPosition = 0x30,
		[Category("Geometry"), Description("Vertical Size")]
		VerticalSize = 0x32,
		[Category("Geometry"), Description("Vertical Pincushion")]
		VerticalPincushion = 0x34,
		[Category("Geometry"), Description("Vertical Pincushion Balance")]
		VerticalPincushionBalance = 0x36,
		[Category("Geometry"), Description("Vertical Convergence R/B")]
		VerticalConvergenceRedBlue = 0x38,
		[Category("Geometry"), Description("Vertical Convergence M/G")]
		VerticalConvergenceMagentaGreen = 0x39,
		[Category("Geometry"), Description("Vertical Linearity")]
		VerticalLinearity = 0x3A,
		[Category("Geometry"), Description("Vertical Linearity Balance")]
		VerticalLinearityBalance = 0x3C,
		[Category("Image"), Description("Clock Phase")]
		ClockPhase = 0x3E,
		[Category("Geometry"), Description("Horizontal Parallelogram")]
		HorizontalParallelogram = 0x40,
		[Category("Geometry"), Description("Vertical Parallelogram")]
		VerticalParallelogram = 0x41,
		[Category("Geometry"), Description("Horizontal Keystone")]
		HorizontalKeystone = 0x42,
		[Category("Geometry"), Description("Vertical Keystone")]
		VerticalKeystone = 0x43,
		[Category("Geometry"), Description("Top Corner Flare")]
		TopCornerFlare = 0x46,
		[Category("Geometry"), Description("Top Corner Hook")]
		TopCornerHook = 0x48,
		[Category("Geometry"), Description("Bottom Corner Flare")]
		BottomCornerFlare = 0x4A,
		[Category("Geometry"), Description("Bottom Corner Hook")]
		BottomCornerHook = 0x4C,
		[Category("Miscellaneous"), Description("Active Control")]
		ActiveControl = 0x52,
		[Category("Miscellaneous"), Description("Performance Preservation")]
		PerformancePreservation = 0x54,
		[Category("Image"), Description("H Moiré")]
		HorizontalMoiré = 0x56,
		[Category("Image"), Description("V Moiré")]
		VerticalMoiré = 0x58,
		[Category("Image"), Description("6 Axis Saturation Control: Red")]
		SixAxisSaturationControlRed = 0x59,
		[Category("Image"), Description("6 Axis Saturation Control: Yellow")]
		SixAxisSaturationControlYellow = 0x5A,
		[Category("Image"), Description("6 Axis Saturation Control: Green")]
		SixAxisSaturationControlGreen = 0x5B,
		[Category("Image"), Description("6 Axis Saturation Control: Cyan")]
		SixAxisSaturationControlCyan = 0x5C,
		[Category("Image"), Description("6 Axis Saturation Control: Blue")]
		SixAxisSaturationControlBlue = 0x5D,
		[Category("Image"), Description("6 Axis Saturation Control: Magenta")]
		SixAxisSaturationControlMagenta = 0x5E,
		[Category("Miscellaneous"), Description("Input Select")]
		InputSelect = 0x60,
		[Category("Audio"), Description("Audio: Speaker Volume")]
		AudioSpeakerVolume = 0x62,
		[Category("Audio"), Description("Audio: Speaker Pair Select")]
		AudioSpeakerPairSelect = 0x63,
		[Category("Audio"), Description("Audio: Microphone Volume")]
		AudioMicrophoneVolume = 0x64,
		[Category("Audio"), Description("Audio: Jack Connection Status")]
		AudioJackConnectionStatus = 0x65,
		[Category("Miscellaneous"), Description("Ambient Light Sensor")]
		AmbientLightSensor = 0x66,
		[Category("Image"), Description("Backlight Level: White")]
		BacklightLevelWhite = 0x6B,
		[Category("Image"), Description("Video Black Level: Red")]
		VideoBlackLevelRed = 0x6C,
		[Category("Image"), Description("Backlight Level: Red")]
		BacklightLevelRed = 0x6D,
		[Category("Image"), Description("Video Black Level: Green")]
		VideoBlackLevelGreen = 0x6E,
		[Category("Image"), Description("Backlight Level: Green")]
		BacklightLevelGreen = 0x6F,
		[Category("Image"), Description("Video Black Level: Blue")]
		VideoBlackLevelBlue = 0x70,
		[Category("Image"), Description("Backlight Level: Blue")]
		BacklightLevelBlue = 0x71,
		[Category("Image"), Description("Gamma")]
		Gamma = 0x72,
		[Category("Image"), Description("LUT Size")]
		LutSize = 0x73,
		[Category("Image"), Description("Single Point LUT Operation")]
		SinglePointLutOperation = 0x74,
		[Category("Image"), Description("Block LUT Operation")]
		BlockLutOperation = 0x75,
		[Category("Miscellaneous"), Description("Remote Procedure Call")]
		RemoteProcedureCall = 0x76,
		[Category("Miscellaneous"), Description("Display Identification Data Operation")]
		DisplayIdentificationDataOperation = 0x78,
		[Category("Image"), Description("Adjust Zoom")]
		AdjustZoom = 0x7C,
		[Category("Geometry"), Description("Horizontal Mirror (Flip)")]
		HorizontalMirror = 0x82,
		[Category("Geometry"), Description("Vertical Mirror (Flip)")]
		VerticalMirror = 0x84,
		[Category("Geometry"), Description("Display Scaling")]
		DisplayScaling = 0x86,
		[Category("Image"), Description("Sharpness")]
		Sharpness = 0x87,
		[Category("Image"), Description("Velocity Scan Modulation")]
		VelocityScanModulation = 0x88,
		[Category("Image"), Description("Color Saturation")]
		ColorSaturation = 0x8A,
		[Category("Miscellaneous"), Description("TV Channel Up / Down")]
		TvChannelUpAndDown = 0x8B,
		[Category("Image"), Description("TV Sharpness")]
		TvSharpness = 0x8C,
		[Category("Audio"), Description("Audio Mute / Screen Blank")]
		AudioMuteAndScreenBlank = 0x8D,
		[Category("Image"), Description("TV Contrast")]
		TvContrast = 0x8E,
		[Category("Audio"), Description("Audio Treble")]
		AudioTreble = 0x8F,
		[Category("Image"), Description("Hue")]
		Hue = 0x90,
		[Category("Audio"), Description("Audio Bass")]
		AudioBass = 0x91,
		[Category("Image"), Description("TV Black Level / Luminance")]
		TvBlackLevel = 0x92,
		[Category("Audio"), Description("Audio Balance L / R")]
		AudioBalanceLeftRight = 0x93,
		[Category("Audio"), Description("Audio Processor Mode")]
		AudioProcessorMode = 0x94,
		[Category("Geometry"), Description("Window Position (TL_X)")]
		WindowPositionLeft = 0x95,
		[Category("Geometry"), Description("Window Position (TL_Y)")]
		WindowPositionTop = 0x96,
		[Category("Geometry"), Description("Window Position (BR_X)")]
		WindowPositionRight = 0x97,
		[Category("Geometry"), Description("Window Position (BR_Y)")]
		WindowPositionBottom = 0x98,
		[Category("Image"), Description("Window Background")]
		WindowBackground = 0x9A,
		[Category("Image"), Description("6 Axis Color Control: Red")]
		SixAxisColorControlRed = 0x9B,
		[Category("Image"), Description("6 Axis Color Control: Yellow")]
		SixAxisColorControlYellow = 0x9C,
		[Category("Image"), Description("6 Axis Color Control: Green")]
		SixAxisColorControlGreen = 0x9D,
		[Category("Image"), Description("6 Axis Color Control: Cyan")]
		SixAxisColorControlCyan = 0x9E,
		[Category("Image"), Description("6 Axis Color Control: Blue")]
		SixAxisColorControlBlue = 0x9F,
		[Category("Image"), Description("6 Axis Color Control: Magenta")]
		SixAxisColorControlMagenta = 0xA0,
		[Category("Image"), Description("Auto Setup On / Off")]
		AutoSetupOnOff = 0xA2,
		[Category("Image"), Description("Window Mask Control")]
		WindowMaskControl = 0xA4,
		[Category("Image"), Description("Window Select")]
		WindowSelect = 0xA5,
		[Category("Image"), Description("Window Size")]
		WindowSize = 0xA6,
		[Category("Image"), Description("Window Transparency")]
		WindowTransparency = 0xA7,
		[Category("Image"), Description("Screen Orientation")]
		ScreenOrientation = 0xAA,
		[Category("Control"), Description("Horizontal Frequency")]
		HorizontalFrequency = 0xAC,
		[Category("Control"), Description("Vertical Frequency")]
		VerticalFrequency = 0xAE,
		[Category("Preset"), Description("Settings")]
		Settings = 0xB0,
		[Category("Miscellaneous"), Description("Flat Panel Sub-Pixel Layout")]
		FlatPanelSubPixelLayout = 0xB2,
		[Category("Control"), Description("Source Timing Mode")]
		SourceTimingMode = 0xB4,
		[Category("Control"), Description("Source Color Coding")]
		SourceColorCoding = 0xB5,
		[Category("Miscellaneous"), Description("Display Technology Type")]
		DisplayTechnologyType = 0xB6,
		[Category("DPVL"), Description("DPVL : Display status")]
		DpvlDisplaystatus = 0xB7,
		[Category("DPVL"), Description("DPVL : Packet count")]
		DpvlPacketcount = 0xB8,
		[Category("DPVL"), Description("DPVL : Display X origin")]
		DpvlDisplayXorigin = 0xB9,
		[Category("DPVL"), Description("DPVL : Display Y origin")]
		DpvlDisplayYorigin = 0xBA,
		[Category("DPVL"), Description("DPVL : Header CRC error count")]
		DpvlHeaderCrcErrorCount = 0xBB,
		[Category("DPVL"), Description("DPVL : Body CRC error count")]
		DpvlBodyCrcErrorCount = 0xBC,
		[Category("DPVL"), Description("DPVL : Client ID")]
		DpvlClientId = 0xBD,
		[Category("DPVL"), Description("DPVL : Link control")]
		DpvlLinkcontrol = 0xBE,
		[Category("Control"), Description("Display Usage Time")]
		DisplayUsageTime = 0xC0,
		[Category("Miscellaneous"), Description("Display Descriptor Length")]
		DisplayDescriptorLength = 0xC2,
		[Category("Miscellaneous"), Description("Transmit Display Descriptor")]
		TransmitDisplayDescriptor = 0xC3,
		[Category("Miscellaneous"), Description("Enable Display of ‘Display Descriptor’")]
		EnableDisplayOfDisplayDescriptor = 0xC4,
		[Category("Miscellaneous"), Description("Application Enable Key")]
		ApplicationEnableKey = 0xC6,
		[Category(""), Description("Reserved")]
		Reserved = 0xC7,
		[Category("Control"), Description("Display Controller ID")]
		DisplayControllerId = 0xC8,
		[Category("Control"), Description("Display Firmware Level")]
		DisplayFirmwareLevel = 0xC9,
		[Category("Control"), Description("OSD")]
		Osd = 0xCA,
		[Category("Control"), Description("OSD Language")]
		OsdLanguage = 0xCC,
		[Category("Miscellaneous"), Description("Status Indicators")]
		StatusIndicators = 0xCD,
		[Category("Miscellaneous"), Description("Auxiliary Display Size")]
		AuxiliaryDisplaySize = 0xCE,
		[Category("Miscellaneous"), Description("Auxiliary Display Data")]
		AuxiliaryDisplayData = 0xCF,
		[Category("Miscellaneous"), Description("Output Selection")]
		OutputSelection = 0xD0,
		[Category("Miscellaneous"), Description("Asset Tag")]
		AssetTag = 0xD2,
		[Category("Image"), Description("Stereo Video Mode")]
		StereoVideoMode = 0xD4,
		[Category("Control"), Description("Power Mode")]
		PowerMode = 0xD6,
		[Category("Miscellaneous"), Description("Auxiliary Power Output")]
		AuxiliaryPowerOutput = 0xD7,
		[Category("Geometry"), Description("Scan Mode")]
		ScanMode = 0xDA,
		[Category("Control"), Description("Image Mode")]
		ImageMode = 0xDB,
		[Category("Image"), Description("Display Application")]
		DisplayApplication = 0xDC,
		[Category("Miscellaneous"), Description("Scratch Pad")]
		ScratchPad = 0xDE,
		[Category("Control"), Description("VCP Version")]
		VcpVersion = 0xDF,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE0 = 0xE0,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE1 = 0xE1,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE2 = 0xE2,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE3 = 0xE3,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE4 = 0xE4,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE5 = 0xE5,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE6 = 0xE6,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE7 = 0xE7,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE8 = 0xE8,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificE9 = 0xE9,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificEA = 0xEA,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificEB = 0xEB,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificEC = 0xEC,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificED = 0xED,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificEE = 0xEE,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificEF = 0xEF,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF0 = 0xF0,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF1 = 0xF1,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF2 = 0xF2,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF3 = 0xF3,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF4 = 0xF4,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF5 = 0xF5,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF6 = 0xF6,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF7 = 0xF7,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF8 = 0xF8,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificF9 = 0xF9,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFA = 0xFA,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFB = 0xFB,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFC = 0xFC,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFD = 0xFD,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFE = 0xFE,
		[Category("Manufacturer Specific"), Description("Manufacturer Specific")]
		ManufacturerSpecificFF = 0xFF,
	}

	public static class VcpCodeExtensions
	{
		private static readonly (string? Name, string? Category, bool IsDefined)[] VcpCodeMetadata = GetVcpCodeMetatada();

		private static (string? Name, string? Category, bool IsDefined)[] GetVcpCodeMetatada()
		{
			var metadata = new (string?, string?, bool)[256];
			for (int i = 0; i <= 255; i++)
			{
				metadata[i] = Enum.GetName((VcpCode)i) is string name && typeof(VcpCode).GetField(name, BindingFlags.Public | BindingFlags.Static) is FieldInfo field
					? (field.GetCustomAttribute<DescriptionAttribute>()!.Description, field.GetCustomAttribute<CategoryAttribute>()!.Category, true)
					: (null, null, false);
			}
			return metadata;
		}

		public static bool IsDefined(this VcpCode code) => VcpCodeMetadata[(int)code].IsDefined;

		public static bool TryGetCategory(this VcpCode code, out string? category)
		{
			ref var metadata = ref VcpCodeMetadata[(int)code];

			category = metadata.Category;
			return metadata.IsDefined;
		}

		public static bool TryGetName(this VcpCode code, out string? name)
		{
			ref var metadata = ref VcpCodeMetadata[(int)code];

			name = metadata.Name;
			return metadata.IsDefined;
		}

		public static bool TryGetNameAndCategory(this VcpCode code, out string? name, out string? category)
		{
			ref var metadata = ref VcpCodeMetadata[(int)code];

			category = metadata.Category;
			name = metadata.Name;
			return metadata.IsDefined;
		}
	}
}
