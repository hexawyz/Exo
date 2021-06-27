namespace DeviceTools.HumanInterfaceDevices.Usages
{
	public enum HidEyeAndHeadTrackerUsage : ushort
	{
		Undefined = 0x00,
		EyeTracker = 0x01,
		HeadTracker = 0x02,

		TrackingData = 0x10,
		Capabilities = 0x11,
		Configuration = 0x12,
		Status = 0x13,
		Control = 0x14,

		SensorTimestamp = 0x20,
		PositionX = 0x21,
		PositionY = 0x22,
		PositionZ = 0x23,
		GazePoint = 0x24,
		LeftEyePosition = 0x25,
		RightEyePosition = 0x26,
		HeadPosition = 0x27,
		HeadDirectionPoint = 0x28,
		RotationAboutXAxis = 0x29,
		RotationAboutYAxis = 0x2A,
		RotationAboutZAxis = 0x2B,

		TrackerQuality = 0x100,
		MinimumTrackingDistance = 0x101,
		OptimumTrackingDistance = 0x102,
		MaximumTrackingDistance = 0x103,
		MaximumScreenPlaneWidth = 0x104,
		MaximumScreenPlaneHeight = 0x105,

		DisplayManufacturerId = 0x200,
		DisplayProductId = 0x201,
		DisplaySerialNumber = 0x202,
		DisplayManufacturerDate = 0x203,
		CalibratedScreenWidth = 0x204,
		CalibratedScreenHeight = 0x205,

		SamplingFrequency = 0x300,
		ConfigurationStatus = 0x301,

		DeviceModeRequest = 0x400,
	}
}
