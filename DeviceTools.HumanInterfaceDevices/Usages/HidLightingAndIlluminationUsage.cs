namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidLightingAndIlluminationUsage : ushort
	{
		Undefined = 0x00,

		LampArray = 0x01,
		LampArrayAttributesReport = 0x02,
		LampCount = 0x03,
		BoundingBoxWidthInMicrometers = 0x04,
		BoundingBoxHeightInMicrometers = 0x05,
		BoundingBoxDepthInMicrometers = 0x06,
		LampArrayKind = 0x07,
		MinUpdateIntervalInMicroseconds = 0x08,

		LampAttributesRequestReport = 0x20,
		LampId = 0x21,
		LampAttributesResponseReport = 0x22,
		PositionXInMicrometers = 0x23,
		PositionYInMicrometers = 0x24,
		PositionZInMicrometers = 0x25,
		LampPurposes = 0x26,
		UpdateLatencyInMicroseconds = 0x27,
		RedLevelCount = 0x28,
		GreenLevelCount = 0x29,
		BlueLevelCount = 0x2A,
		IntensityLevelCount = 0x2B,
		IsProgrammable = 0x2C,
		InputBinding = 0x2D,

		LampMultiUpdateReport = 0x50,
		RedUpdateChannel = 0x51,
		GreenUpdateChannel = 0x52,
		BlueUpdateChannel = 0x53,
		IntensityUpdateChannel = 0x54,
		LampUpdateFlags = 0x55,

		LampRangeUpdateReport = 0x60,
		LampIdStart = 0x61,
		LampIdEnd = 0x62,

		LampArrayControlReport = 0x70,
		AutonomousMode = 0x71,
	}
}
