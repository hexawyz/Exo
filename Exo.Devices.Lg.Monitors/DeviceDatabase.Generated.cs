namespace Exo.Devices.Lg.Monitors;

// We should in theory be able to retrieve the device IDs by querying the device, but even if technically possible, it is not straightforward, and not guaranteed exhaustive.
// Instead, we'll provide here a list of DeviceIds for each device.
partial class DeviceDatabase
{
	private const ushort UsbMonitorCount = 1;

	// Index table mapping product ID => Detail Index. Only regular PNP IDs here, as USB IDs can be shared for multiple monitor models.
	private static ReadOnlySpan<byte> Index =>
	[
		0xBF, 0x5B, 0x00, 0x00, 0xC0, 0x5B, 0x00, 0x00, 0xEE, 0x5B, 0x00, 0x00,
	];

	// Product ID details giving pointers into Product IDs and Names. USB Details are in the first part.
	private static ReadOnlySpan<byte> Details =>
	[
		0x00, 0x00, 0x00, 0x00, 0x04, 0x00,
	];

	// Product ID sequences. Ordered by detail index, but this is an implementation detail.
	private static ReadOnlySpan<byte> ProductIds =>
	[
		0x8A, 0x9A, 0xBF, 0x5B, 0xEE, 0x5B, 0xC0, 0x5B,
	];

	// Unique strings.
	private static readonly string[] Strings =
	[
		@"27GP950",
	];

	private static readonly Dictionary<string, ushort> MonitorIndicesByName = new()
	{
		{ @"27GP950", 0 },
	};
}

