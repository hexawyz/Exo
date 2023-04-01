namespace DeviceTools.Logitech.HidPlusPlus;

public interface IUsbReceiver
{
	// TODO: Add device pairing events.

	/// <summary>Occurs when the device is first discovered on the receiver.</summary>
	/// <remarks>
	/// The device may or may not be actively connected when discovered.
	/// If the device is discovered connected, the <see cref="DeviceConnected"/> event will also be raised.
	/// </remarks>
	event ReceiverDeviceEventHandler? DeviceDiscovered;

	/// <summary>Occurs when a device is newly connected.</summary>
	event ReceiverDeviceEventHandler? DeviceConnected;
	/// <summary>Occurs when a previously connected device is disconnected.</summary>
	event ReceiverDeviceEventHandler? DeviceDisconnected;
}
