namespace DeviceTools.Logitech.HidPlusPlus;

public static class ProductCategoryExtensions
{
	public static DeviceConnectionType InferConnectionType(this ProductCategory category)
		=> category switch
		{
			ProductCategory.VirtualUsbGameController or
			ProductCategory.UsbScanner or
			ProductCategory.UsbCamera or
			ProductCategory.UsbAudio or
			ProductCategory.UsbHub or
			ProductCategory.UsbSpecial or
			ProductCategory.UsbMouse or
			ProductCategory.UsbRemoteControl or
			ProductCategory.UsbPcGamingDevice or
			ProductCategory.UsbKeyboard or
			ProductCategory.UsbTrackBall or
			ProductCategory.Usb3dControlDevice or
			ProductCategory.UsbOtherPointingDevice or
			ProductCategory.UsbConsoleGamingDevice or
			ProductCategory.UsbToolsCorded => DeviceConnectionType.Corded,
			ProductCategory.UsbBluetoothReceiver or
			ProductCategory.UsbReceiver or
			ProductCategory.QuadMouseTransceiver or
			ProductCategory.QuadDesktopTransceiver or
			ProductCategory.UsbToolsTransceiver => DeviceConnectionType.Transceiver,
			ProductCategory.BluetoothMouse or
			ProductCategory.BluetoothKeyboard or
			ProductCategory.BluetoothRemoteControl or
			ProductCategory.BluetoothReserved or
			ProductCategory.BluetoothAudio or
			ProductCategory.QuadFapDevice or
			ProductCategory.QuadMouse or
			ProductCategory.QuadKeyboard => DeviceConnectionType.Wireless,
			_ => DeviceConnectionType.Unknown,
		};

	public static RegisterAccessProtocol.DeviceType InferDeviceType(this ProductCategory category)
		=> category switch
		{
			ProductCategory.BluetoothKeyboard or
			ProductCategory.UsbKeyboard or
			ProductCategory.QuadKeyboard => RegisterAccessProtocol.DeviceType.Keyboard,
			ProductCategory.BluetoothMouse or
			ProductCategory.UsbMouse or
			ProductCategory.QuadMouse => RegisterAccessProtocol.DeviceType.Mouse,
			ProductCategory.BluetoothNumpad => RegisterAccessProtocol.DeviceType.Numpad,
			ProductCategory.UsbTrackBall => RegisterAccessProtocol.DeviceType.Trackball,
			_ => RegisterAccessProtocol.DeviceType.Unknown,
		};
}
