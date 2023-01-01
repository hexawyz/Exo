using DeviceTools.FilterExpressions;
using static DeviceTools.NativeMethods;

namespace DeviceTools.Properties
{
	public static class System
	{
		public static class Device
		{
			public static readonly StringProperty PrinterURL = new(new(0x0b48f35A, 0xbe6e, 0x4f17, 0xb1, 0x08, 0x3c, 0x40, 0x73, 0xd1, 0x66, 0x9a), 15);
		}

		public static class DeviceInterface
		{
			public static class Bluetooth
			{
				public static readonly StringProperty DeviceAddress = new(ShellPropertyCategoryGuids.Bluetooth, 1);
				public static readonly GuidProperty ServiceGuid = new(ShellPropertyCategoryGuids.Bluetooth, 2);
				public static readonly UInt32Property Flags = new(ShellPropertyCategoryGuids.Bluetooth, 3);
				public static readonly StringProperty Manufacturer = new(ShellPropertyCategoryGuids.Bluetooth, 4);
				public static readonly StringProperty ModelNumber = new(ShellPropertyCategoryGuids.Bluetooth, 5);
				public static readonly ByteProperty VendorIdSource = new(ShellPropertyCategoryGuids.Bluetooth, 6);
				public static readonly UInt16Property VendorId = new(ShellPropertyCategoryGuids.Bluetooth, 7);
				public static readonly UInt16Property ProductId = new(ShellPropertyCategoryGuids.Bluetooth, 8);
				public static readonly UInt16Property ProductVersion = new(ShellPropertyCategoryGuids.Bluetooth, 9);
				//public static readonly UInt32Property ClassOfDevice = new(ShellPropertyCategoryGuids.Bluetooth, 10);
				public static readonly FileTimeProperty LastConnectedTime = new(ShellPropertyCategoryGuids.Bluetooth, 11);
			}

			public static class Hid
			{
				public static readonly UInt16Property UsagePage = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 2);
				public static readonly UInt16Property UsageId = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 3);
				public static readonly BooleanProperty IsReadOnly = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 4);
				public static readonly UInt16Property VendorId = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 5);
				public static readonly UInt16Property ProductId = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 6);
				public static readonly UInt16Property VersionNumber = new(ShellPropertyCategoryGuids.DeviceInterfaceClassGuid, 7);
			}

			public static readonly StringProperty PrinterDriverDirectory = new(new(0x847c66de, 0xb8d6, 0x4af9, 0xab, 0xc3, 0x6f, 0x4f, 0x92, 0x6b, 0xc0, 0x39), 14);
			public static readonly StringProperty PrinterDriverName = new(new(0xafc47170, 0x14f5, 0x498c, 0x8f, 0x30, 0xb0, 0xd1, 0x9b, 0xe4, 0x49, 0xc6), 11);
			public static readonly UInt32Property PrinterEnumerationFlag = new(new(0xa00742a1, 0xcd8c, 0x4b37, 0x95, 0xab, 0x70, 0x75, 0x55, 0x87, 0x76, 0x7a), 3);
			public static readonly StringProperty PrinterName = new(new(0x0a7b84ef, 0x0c27, 0x463f, 0x84, 0xef, 0x06, 0xc5, 0x07, 0x00, 0x01, 0xbe), 10);
			public static readonly StringProperty PrinterPortName = new(new(0xeec7b761, 0x6f94, 0x41b1, 0x94, 0x9f, 0xc7, 0x29, 0x72, 0x0d, 0xd1, 0x3c), 12);

			public static class Proximity
			{
				public static readonly BooleanProperty SupportsNfc = new(new(0xfb3842cd, 0x9e2a, 0x4f83, 0x8f, 0xcc, 0x4b, 0x07, 0x61, 0x13, 0x9a, 0xe9), 2);
			}

			public static class Serial
			{
				public static readonly UInt16Property UsbVendorId = new(ShellPropertyCategoryGuids.Serial, 2);
				public static readonly UInt16Property UsbProductId = new(ShellPropertyCategoryGuids.Serial, 3);
				public static readonly StringProperty PortName = new(ShellPropertyCategoryGuids.Serial, 4);
			}

			public static class WinUsb
			{
				public static readonly UInt16Property UsbVendorId = new(ShellPropertyCategoryGuids.WinUsb, 2);
				public static readonly UInt16Property UsbProductId = new(ShellPropertyCategoryGuids.WinUsb, 3);
				public static readonly ByteProperty UsbClass = new(ShellPropertyCategoryGuids.WinUsb, 4);
				public static readonly ByteProperty UsbSubClass = new(ShellPropertyCategoryGuids.WinUsb, 5);
				public static readonly ByteProperty UsbProtocol = new(ShellPropertyCategoryGuids.WinUsb, 6);
				public static readonly StringListProperty DeviceInterfaceClasses = new(ShellPropertyCategoryGuids.WinUsb, 7);
			}
		}

		public static class Devices
		{
			public static class Aep
			{
			}

			public static class AudioDevice
			{
			}

			public static readonly ByteProperty SignalStrength = new(ShellPropertyCategoryGuids.Smartphone, 2);
			public static readonly ByteProperty TextMessages = new(ShellPropertyCategoryGuids.Smartphone, 3);
			public static readonly UInt16Property NewPictures = new(ShellPropertyCategoryGuids.Smartphone, 4);
			public static readonly ByteProperty MissedCalls = new(ShellPropertyCategoryGuids.Smartphone, 5);
			public static readonly ByteProperty Voicemail = new(ShellPropertyCategoryGuids.Smartphone, 6);
			public static readonly StringProperty NetworkName = new(ShellPropertyCategoryGuids.Smartphone, 7);
			public static readonly StringProperty NetworkType = new(ShellPropertyCategoryGuids.Smartphone, 8);
			public static readonly ByteProperty Roaming = new(ShellPropertyCategoryGuids.Smartphone, 9);
			public static readonly ByteProperty BatteryLife = new(ShellPropertyCategoryGuids.Smartphone, 10);
			public static readonly ByteProperty ChargingState = new(ShellPropertyCategoryGuids.Smartphone, 11);
			public static readonly UInt64Property StorageCapacity = new(ShellPropertyCategoryGuids.Smartphone, 12);
			public static readonly UInt64Property StorageFreeSpace = new(ShellPropertyCategoryGuids.Smartphone, 13);
			public static readonly UInt32Property StorageFreeSpacePercent = new(ShellPropertyCategoryGuids.Smartphone, 14);
			public static readonly ByteProperty BatteryPlusCharging = new(ShellPropertyCategoryGuids.Smartphone, 22);
			public static readonly StringProperty BatteryPlusChargingText = new(ShellPropertyCategoryGuids.Smartphone, 23);

			public static readonly StringListProperty DiscoveryMethod = new(ShellPropertyCategoryGuids.DeviceContainer, 52);
			public static readonly BooleanProperty Connected = new(ShellPropertyCategoryGuids.DeviceContainer, 55);
			public static readonly BooleanProperty Paired = new(ShellPropertyCategoryGuids.DeviceContainer, 56);
			public static readonly StringProperty Icon = new(ShellPropertyCategoryGuids.DeviceContainer, 57);
			public static readonly BooleanProperty LocalMachine = new(ShellPropertyCategoryGuids.DeviceContainer, 70);
			public static readonly StringProperty MetadataPath = new(ShellPropertyCategoryGuids.DeviceContainer, 71);
			public static readonly BooleanProperty LaunchDeviceStageFromExplorer = new(ShellPropertyCategoryGuids.DeviceContainer, 77);
			public static readonly StringProperty DeviceDescription1 = new(ShellPropertyCategoryGuids.DeviceContainer, 81);
			public static readonly StringProperty DeviceDescription2 = new(ShellPropertyCategoryGuids.DeviceContainer, 82);
			public static readonly BooleanProperty NotWorkingProperly = new(ShellPropertyCategoryGuids.DeviceContainer, 83);
			public static readonly BooleanProperty IsShared = new(ShellPropertyCategoryGuids.DeviceContainer, 84);
			public static readonly BooleanProperty IsNetworkConnected = new(ShellPropertyCategoryGuids.DeviceContainer, 85);
			public static readonly BooleanProperty IsDefault = new(ShellPropertyCategoryGuids.DeviceContainer, 86);
			public static readonly StringListProperty CategoryIds = new(ShellPropertyCategoryGuids.DeviceContainer, 90);
			public static readonly StringListProperty Category = new(ShellPropertyCategoryGuids.DeviceContainer, 91);
			public static readonly StringListProperty CategoryPlural = new(ShellPropertyCategoryGuids.DeviceContainer, 92);
			public static readonly StringListProperty CategoryGroup = new(ShellPropertyCategoryGuids.DeviceContainer, 94);
			public static readonly StringProperty DeviceInstanceId = new(ShellPropertyCategoryGuids.DeviceContainer, 256);

			public static readonly StringProperty Parent = new(new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), 8);
			public static readonly StringListProperty Children = new(new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7), 9);

			public static readonly StringProperty SharedTooltip = new(new(0x880f70a2, 0x6082, 0x47ac, 0x8a, 0xab, 0xa7, 0x39, 0xd1, 0xa3, 0x00, 0xc3), 151);
			public static readonly StringProperty NetworkedTooltip = new(new(0x880f70a2, 0x6082, 0x47ac, 0x8a, 0xab, 0xa7, 0x39, 0xd1, 0xa3, 0x00, 0xc3), 152);
			public static readonly StringProperty DefaultTooltip = new(new(0x880f70a2, 0x6082, 0x47ac, 0x8a, 0xab, 0xa7, 0x39, 0xd1, 0xa3, 0x00, 0xc3), 153);

			public static readonly BooleanProperty Present = new(ShellPropertyCategoryGuids.DeviceOther, 5);
			public static readonly BooleanProperty DeviceHasProblem = new(ShellPropertyCategoryGuids.DeviceOther, 6);
			public static readonly BinaryProperty PhysicalDeviceLocation = new(ShellPropertyCategoryGuids.DeviceOther, 9);

			public static readonly StringProperty Manufacturer = new(ShellPropertyCategoryGuids.DeviceContainer2, 8192);
			public static readonly StringProperty ModelNumber = new(ShellPropertyCategoryGuids.DeviceContainer2, 8195);
			public static readonly StringProperty PresentationUrl = new(ShellPropertyCategoryGuids.DeviceContainer2, 8198);
			public static readonly StringListProperty ServiceAddress = new(ShellPropertyCategoryGuids.DeviceContainer2, 16384);
			public static readonly StringProperty ServiceId = new(ShellPropertyCategoryGuids.DeviceContainer2, 16385);
			public static readonly StringProperty FriendlyName = new(ShellPropertyCategoryGuids.DeviceContainer2, 12288);
			public static readonly StringListProperty IpAddress = new(ShellPropertyCategoryGuids.DeviceContainer2, 12297);

			public static readonly StringListProperty InterfacePaths = new(new(0xd08dd4c0, 0x3a9e, 0x462e, 0x82, 0x90, 0x7b, 0x63, 0x6b, 0x25, 0x76, 0xb9), 2);
			public static readonly StringListProperty FunctionPaths = new(new(0xd08dd4c0, 0x3a9e, 0x462e, 0x82, 0x90, 0x7b, 0x63, 0x6b, 0x25, 0x76, 0xb9), 3);
			public static readonly StringProperty Status1 = new(new(0xd08dd4c0, 0x3a9e, 0x462e, 0x82, 0x90, 0x7b, 0x63, 0x6b, 0x25, 0x76, 0xb9), 257);
			public static readonly StringProperty Status2 = new(new(0xd08dd4c0, 0x3a9e, 0x462e, 0x82, 0x90, 0x7b, 0x63, 0x6b, 0x25, 0x76, 0xb9), 258);
			public static readonly StringListProperty Status = new(new(0xd08dd4c0, 0x3a9e, 0x462e, 0x82, 0x90, 0x7b, 0x63, 0x6b, 0x25, 0x76, 0xb9), 259);

			public static readonly StringProperty GlyphIcon = new(new(0x51236583, 0x0c4a, 0x4fe8, 0xb8, 0x1f, 0x16, 0x6a, 0xec, 0x13, 0xf5, 0x10), 123);

			public static readonly BooleanProperty IsSoftwareInstalling = new(new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29), 9);

			// Type == object ??
			public static readonly PropertyKey NotificationStore = new(new(0x06704b0c, 0xe830, 0x4c81, 0x91, 0x78, 0x91, 0xe4, 0xe9, 0x5a, 0x80, 0xa0), 2);
			public static readonly StringProperty Notification = new(new(0x06704b0c, 0xe830, 0x4c81, 0x91, 0x78, 0x91, 0xe4, 0xe9, 0x5a, 0x80, 0xa0), 3);

			public static readonly ByteProperty PlaybackState = new(new(0x3633de59, 0x6825, 0x4381, 0xa4, 0x9b, 0x9f, 0x6b, 0xa1, 0x3a, 0x14, 0x71), 2);
			public static readonly StringProperty PlaybackTitle = new(new(0x3633de59, 0x6825, 0x4381, 0xa4, 0x9b, 0x9f, 0x6b, 0xa1, 0x3a, 0x14, 0x71), 3);
			public static readonly UInt64Property RemainingDuration = new(new(0x3633de59, 0x6825, 0x4381, 0xa4, 0x9b, 0x9f, 0x6b, 0xa1, 0x3a, 0x14, 0x71), 4);
			public static readonly UInt32Property PlaybackPositionPercent = new(new(0x3633de59, 0x6825, 0x4381, 0xa4, 0x9b, 0x9f, 0x6b, 0xa1, 0x3a, 0x14, 0x71), 5);

			public static readonly BooleanProperty SafeRemovalRequired = new(new(0xafd97640, 0x86a3, 0x4210, 0xb6, 0x7c, 0x28, 0x9c, 0x41, 0xaa, 0xbe, 0x55), 2);

			public static readonly BooleanProperty InterfaceEnabled = new(ShellPropertyCategoryGuids.DeviceInterface, 3);
			public static readonly GuidProperty InterfaceClassGuid = new(ShellPropertyCategoryGuids.DeviceInterface, 4);
			public static readonly BooleanProperty RestrictedInterface = new(ShellPropertyCategoryGuids.DeviceInterface, 6);

			public static readonly GuidProperty ContainerId = new(ShellPropertyCategoryGuids.DeviceContainerMapping, 2);
			public static readonly BooleanProperty InLocalMachineContainer = new(ShellPropertyCategoryGuids.DeviceContainerMapping, 4);

			public static readonly UInt32Property WiaDeviceType = new(DeviceClassGuids.Image, 2);

			//public static readonly StringProperty DeviceDesc = new(ShellPropertyCategoryGuids.Device, 2);
			public static readonly StringListProperty HardwareIds = new(ShellPropertyCategoryGuids.Device, 3);
			public static readonly StringListProperty CompatibleIds = new(ShellPropertyCategoryGuids.Device, 4);
			//public static readonly StringProperty Service = new(ShellPropertyCategoryGuids.Device, 6);
			//public static readonly StringProperty Class = new(ShellPropertyCategoryGuids.Device, 9);
			public static readonly GuidProperty ClassGuid = new(ShellPropertyCategoryGuids.Device, 10);
			//public static readonly StringProperty Driver = new(ShellPropertyCategoryGuids.Device, 11);
			//public static readonly UInt32Property ConfigFlags = new(ShellPropertyCategoryGuids.Device, 12);
			public static readonly StringProperty DeviceManufacturer = new(ShellPropertyCategoryGuids.Device, 13);
			//public static readonly StringProperty FriendlyName = new(ShellPropertyCategoryGuids.Device, 14);
			//public static readonly StringProperty LocationInfo = new(ShellPropertyCategoryGuids.Device, 15);
			//public static readonly StringProperty PhysicalDeviceObjectName = new(ShellPropertyCategoryGuids.Device, 16);
			public static readonly UInt32Property DeviceCapabilities = new(ShellPropertyCategoryGuids.Device, 17);
			//public static readonly UInt32Property UiNumber = new(ShellPropertyCategoryGuids.Device, 18);
			//public static readonly StringListProperty UpperFilters = new(ShellPropertyCategoryGuids.Device, 19);
			//public static readonly StringListProperty LowerFilters = new(ShellPropertyCategoryGuids.Device, 20);
			//public static readonly GuidProperty BusTypeGuid = new(ShellPropertyCategoryGuids.Device, 21);
			//public static readonly UInt32Property LegacyBusType = new(ShellPropertyCategoryGuids.Device, 22);
			//public static readonly UInt32Property BusNumber = new(ShellPropertyCategoryGuids.Device, 23);
			//public static readonly StringProperty EnumeratorName = new(ShellPropertyCategoryGuids.Device, 24);
			////public static readonly PropertyKey Security = new(ShellPropertyCategoryGuids.Device, 25);
			////public static readonly PropertyKey SecurityDescriptorString = new(ShellPropertyCategoryGuids.Device, 26);
			//public static readonly UInt32Property DeviceType = new(ShellPropertyCategoryGuids.Device, 27);
			//public static readonly BooleanProperty Exclusive = new(ShellPropertyCategoryGuids.Device, 28);
			public static readonly UInt32Property DeviceCharacteristics = new(ShellPropertyCategoryGuids.Device, 29);
			//public static readonly UInt32Property Address = new(ShellPropertyCategoryGuids.Device, 30);
			//public static readonly StringProperty UiNumberDescFormat = new(ShellPropertyCategoryGuids.Device, 31);
			//public static readonly BinaryProperty PowerData = new(ShellPropertyCategoryGuids.Device, 32);
			//public static readonly UInt32Property RemovalPolicy = new(ShellPropertyCategoryGuids.Device, 33);
			//public static readonly UInt32Property RemovalPolicyDefault = new(ShellPropertyCategoryGuids.Device, 34);
			//public static readonly UInt32Property RemovalPolicyOverride = new(ShellPropertyCategoryGuids.Device, 35);
			//public static readonly UInt32Property InstallState = new(ShellPropertyCategoryGuids.Device, 36);
			public static readonly StringListProperty LocationPaths = new(ShellPropertyCategoryGuids.Device, 37);
			//public static readonly GuidProperty BaseContainerId = new(ShellPropertyCategoryGuids.Device, 38);

			public static readonly UInt32Property DevObjectType = new(new(0x13673f42, 0xa3d6, 0x49f6, 0xb4, 0xda, 0xae, 0x46, 0xe0, 0xc5, 0x23, 0x7c), 2);
		}

		public static readonly StringProperty ItemNameDisplay = new(ShellPropertyCategoryGuids.Storage, 10);
	}
}
