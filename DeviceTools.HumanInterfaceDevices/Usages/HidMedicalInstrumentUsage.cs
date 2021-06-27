namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidMedicalInstrumentUsage : ushort
	{
		Undefined = 0x00,

		MedicalUltrasound = 0x01,

		VcrOrAcquisition = 0x20,
		FreezeOrThaw = 0x21,
		ClipStore = 0x22,
		Update = 0x23,
		Next = 0x24,
		Save = 0x25,
		Print = 0x26,
		MicrophoneEnable = 0x27,

		Cine = 0x40,
		TransmitPower = 0x41,
		Volume = 0x42,
		Focus = 0x43,
		Depth = 0x44,

		SoftStepPriamry = 0x60,
		SoftStepSecondary = 0x61,

		DepthGainCompensation = 0x70,

		ZoomSelect = 0x80,
		ZoomAdjust = 0x81,
		SpectralDopplerModeSelect = 0x82,
		SpectralDopplerAdjust = 0x83,
		ColorDopplerModeSelect = 0x84,
		ColorDopplerAdjust = 0x85,
		MotionModeSelect = 0x86,
		MotionModeAdjust = 0x87,
		MotionMode2DSelect = 0x88,
		MotionMode2DAdjust = 0x89,

		SoftControlSelect = 0xA0,
		SoftControlAdjust = 0xA1,
	}
}
