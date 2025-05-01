namespace Exo.Devices.Nzxt;

public readonly partial struct LightingAccessoryInformation
{
	// Quick and dirty; to be improved later.
	public static bool TryGet(byte accessoryId, out LightingAccessoryInformation info)
	{
		switch (accessoryId)
		{
		// Kraken X Ring
		case 0x10:
			// Judging from CAM screenshots online, the ring should have 8 LEDs
			info = new(accessoryId, 8, "Ring");
			return true;
		// Kraken X Logo
		case 0x11:
			// Assuming that logo is a single LED, which should be reasonable.
			info = new(accessoryId, 1, "Logo");
			return true;
		case 0x13:
			info = new(accessoryId, 18, "F120 RGB");
			return true;
		case 0x14:
			info = new(accessoryId, 18, "F140 RGB");
			return true;
		default:
			info = default;
			return false;
		}
	}

	private LightingAccessoryInformation(byte accessoryId, byte ledCount, string name)
	{
		AccessoryId = accessoryId;
		LedCount = ledCount;
		Name = name;
	}

	public byte AccessoryId { get; }
	public byte LedCount { get; }

	// The name will only be used internally. We don't strictly need it but it will make for nicer logging.
	public string Name { get; }

	public Guid GetZoneId(byte channelIndex, byte accessoryIndex)
		=> AccessoryId switch
		{
			0x10 => RingZoneId,
			0x11 => LogoZoneId,
			0x13 => F120RgbZoneIds[GetZoneIdIndex(channelIndex, accessoryIndex)],
			0x14 => F140RgbZoneIds[GetZoneIdIndex(channelIndex, accessoryIndex)],
			_ => throw new NotSupportedException(),
		};

	private static int GetZoneIdIndex(byte channelIndex, byte accessoryIndex)
		=> (channelIndex - 1) * 6 + (accessoryIndex - 1);
}
