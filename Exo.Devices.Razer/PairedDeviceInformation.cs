namespace Exo.Devices.Razer;

public readonly struct PairedDeviceInformation
{
	public PairedDeviceInformation(bool isConnected, ushort productId)
	{
		IsConnected = isConnected;
		ProductId = productId;
	}

	public bool IsConnected { get; }
	public ushort ProductId { get; }
}
