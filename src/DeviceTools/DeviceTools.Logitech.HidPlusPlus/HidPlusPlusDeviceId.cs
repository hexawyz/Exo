namespace DeviceTools.Logitech.HidPlusPlus;

public readonly record struct HidPlusPlusDeviceId
{
	public DeviceIdSource Source { get; init; }
	public ushort ProductId { get; init; }

	public HidPlusPlusDeviceId(DeviceIdSource source, ushort productId)
	{
		Source = source;
		ProductId = productId;
	}
}
