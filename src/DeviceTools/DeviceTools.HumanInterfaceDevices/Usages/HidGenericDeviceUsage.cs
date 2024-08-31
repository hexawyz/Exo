namespace DeviceTools.HumanInterfaceDevices.Usages
{
    public enum HidGenericDeviceUsage : ushort
    {
        Undefined = 0x00,
		BackgroundOrNonUserControls = 0x01,

        BatteryStrength = 0x20,
        WirelessChannel = 0x21,
        WirelessId = 0x22,
        DiscoverWirelessControl = 0x23,
        SecurityCodeCharacterEntered = 0x24,
        SecurityCodeCharacterErased = 0x25,
        SecurityCodeCleared = 0x26,

		SequenceId = 0x27,
		SequenceIdReset = 0x28,

		RfSignalStrength = 0x29,

		SoftwareVersion = 0x2A,
		ProtocolVersion = 0x2B,
		HardwareVersion = 0x2C,
		Major = 0x2D,
		Minor = 0x2E,
		Revision = 0x2F,

		Handedness = 0x30,
		EitherHand = 0x31,
		LeftHand = 0x32,
		RightHand = 0x33,
		BothHand = 0x34,

		GripPoseOffset = 0x40,
		PointerPoseOffset = 0x41,
	}
}
