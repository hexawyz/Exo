namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidHapticUsage : ushort
	{
		Undefined = 0x00,
		SimpleHapticController = 0x01,

		WaveformList = 0x10,
		DurationList = 0x11,

		AutoTrigger = 0x20,
		ManualTrigger = 0x21,
		AutoTriggerAssociatedControl = 0x22,
		Intensity = 0x23,
		RepeatCount = 0x24,
		RetriggerPeriod = 0x25,
		WaveformVendorPage = 0x26,
		WaveformVendorId = 0x27,
		WaveformCutoffTime = 0x28,

		WaveformNone = 0x1001,
		WaveformStop = 0x1002,
		WaveformClick = 0x1003,
		WaveformBuzzContinuous = 0x1004,
		WaveformRumbleContinuous = 0x1005,
		WaveformPress = 0x1006,
		WaveformRelease = 0x1007,
	}
}
