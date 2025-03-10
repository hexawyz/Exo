using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

// For simplicity, this must be kept in sync with Exo.DeviceCategory.

[DataContract]
public enum DeviceCategory
{
	[EnumMember]
	Other = 0,
	[EnumMember]
	Motherboard,
	[EnumMember]
	MemoryModule,
	[EnumMember]
	Processor,
	[EnumMember]
	Cooler,
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
	Light,
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
	PowerSupply,
	[EnumMember]
	MouseDock,
	[EnumMember]
	MousePad,
}
