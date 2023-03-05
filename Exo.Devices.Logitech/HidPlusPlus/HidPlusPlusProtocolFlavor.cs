namespace Exo.Devices.Logitech.HidPlusPlus;

public enum HidPlusPlusProtocolFlavor : byte
{
	/// <summary>Indicates that the protocol used by the device is not yet known.</summary>
	Unknown = 0,
	/// <summary>This indicates an HID++ 1.0 device. (Register Access Protocol)</summary>
	RegisterAccess = 1,
	/// <summary>This indicates an HID++ 2.0 device. (Feature Access Protocol)</summary>
	FeatureAccess = 2,
	/// <summary>This indicates an HID++ 2.0 device transmitting data through an HID++ 1.0 device. (e.g Unifying Receiver)</summary>
	FeatureAccessOverRegisterAccess = 3,
}
