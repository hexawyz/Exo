namespace Exo;

// A list of device categories that may be relevant.
// New items can be added as needed.
/// <summary>Represents the category associated with a device.</summary>
/// <remarks>The device category does not determine the features actually exposed by a device driver, but it is used to classify devices for user friendliness.</remarks>
// NB: Should be kept in sync with the enumeration in Exo.Contracts.Ui.Settings.
public enum DeviceCategory : byte
{
	Other = 0,
	Motherboard,
	MemoryModule,
	Processor,
	Cooler,
	Usb,
	Keyboard,
	Numpad,
	Mouse,
	Touchpad,
	Gamepad,
	Monitor,
	GraphicsAdapter,
	// Light means a "light" in the sense of a something that can be turned on and off by multiple sources, like a wireless switch for example, thus behaving more like a lightbulb.
	Light,
	// Lighting is the general category intended for RGB lighting or everything that could be assimilated to it. (Even a single white led would fall into this)
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
	Battery,
	PowerSupply,
	MouseDock,
	MousePad,
}
