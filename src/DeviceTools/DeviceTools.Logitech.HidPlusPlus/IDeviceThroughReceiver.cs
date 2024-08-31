namespace DeviceTools.Logitech.HidPlusPlus;

public interface IDeviceThroughReceiver
{
	byte DeviceIndex { get; }
	bool IsConnected { get; }
	event DeviceEventHandler? Connected;
	event DeviceEventHandler? Disconnected;
}
