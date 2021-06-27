namespace DeviceTools.HumanInterfaceDevices.Lamps
{
	public enum LampArrayKind : uint
	{
		Undefined = 0x00,
		/// <summary>LampArray is part of a keyboard/keypad device.</summary>
		LampArrayKindKeyboard = 0x01,
		/// <summary>LampArray is part of a mouse.</summary>
		LampArrayKindMouse = 0x02,
		/// <summary>LampArray is part of a game-controller. (e.g. gamepad, flightstick, sailing simulation device)</summary>
		LampArrayKindGameController = 0x03,
		/// <summary>LampArray is part of a general peripheral/accessory (e.g. speakers, mousepad, microphone, webcam)</summary>
		LampArrayKindPeripheral = 0x04,
		/// <summary>LampArray illuminates a room/performance-stage/area (e.g. room light-bulbs, spotlights, washlights, strobelights, booth-strips, billboard/sign, camera-flash)</summary>
		LampArrayKindScene = 0x05,
		/// <summary>LampArray is part of a notification device.</summary>
		LampArrayKindNotification = 0x06,
		/// <summary>LampArray is part of an internal PC case component (e.g. RAM-stick, motherboard, fan)</summary>
		LampArrayKindChassis = 0x07,
		/// <summary>LampArray is embedded in a wearable accessory (audio-headset, wristband, watch, shoes)</summary>
		LampArrayKindWearable = 0x08,
		/// <summary>LampArray is embedded in a piece of funiture (e.g. chair, desk, bookcase)</summary>
		LampArrayKindFurniture = 0x09,
		/// <summary>LampArray is embedded in an artwork (e.g. painting, sculpture)</summary>
		LampArrayKindArt = 0x0A,
	}
}
