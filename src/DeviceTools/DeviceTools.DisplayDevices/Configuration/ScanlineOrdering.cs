namespace DeviceTools.DisplayDevices.Configuration
{
	// This enumeration maps native platform values for DISPLAYCONFIG_SCANLINE_ORDERING.
	public enum ScanlineOrdering
	{
		Unspecified = 0,
		Progressive = 1,
		Interlaced = 2,
		InterlacedUpperFieldFirst = Interlaced,
		InterlacedLowerFieldFirst = 3,
	}
}
