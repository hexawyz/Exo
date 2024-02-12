using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

// For simplicity, this must be kept in sync with Exo.DeviceCategory.

[DataContract]
public enum DeviceCategory
{
	[EnumMember]
	Other = 0,
	[EnumMember]
	Usb,
	[EnumMember]
	Keyboard,
	[EnumMember]
	Numpad,
	[EnumMember]
	Mouse,
	[EnumMember]
	Touchpad,
	[EnumMember]
	Gamepad,
	[EnumMember]
	Monitor,
	[EnumMember]
	GraphicsAdapter,
	[EnumMember]
	Lighting,
	[EnumMember]
	UsbWirelessNetwork,
	[EnumMember]
	UsbWirelessReceiver,
	[EnumMember]
	Speaker,
	[EnumMember]
	Headphone,
	[EnumMember]
	Microphone,
	[EnumMember]
	Headset,
	[EnumMember]
	WirelessSpeaker,
	[EnumMember]
	AudioMixer,
	[EnumMember]
	Webcam,
	[EnumMember]
	Camera,
	[EnumMember]
	Smartphone,
	[EnumMember]
	Battery,
	[EnumMember]
	MouseDock,
	[EnumMember]
	MousePad,
}
