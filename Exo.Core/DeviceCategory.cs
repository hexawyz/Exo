namespace Exo;

// A list of device categories that may be relevant.
// New items can be added as needed.
/// <summary>Represents the category associated with a device.</summary>
/// <remarks>The device category does not determine the features actually exposed by a device driver, but it is used to classify devices for user friendliness.</remarks>
// NB: Should be kept in sync with the enumeration in Exo.Ui.Contracts.
public enum DeviceCategory
{
	Other = 0,
	Usb,
	Keyboard,
	Numpad,
	Mouse,
	Touchpad,
	Gamepad,
	Monitor,
	Lighting,
	UsbWirelessNetwork,
	UsbWirelessReceiver,
	Speaker,
	Headphone,
	Microphone,
	Headset,
	WirelessSpeaker,
	AudioMixer,
	Webcam,
	Camera,
	Smartphone,
}
