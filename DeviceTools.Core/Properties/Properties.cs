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
				public static readonly GuidProperty ServiceGUID = new(ShellPropertyCategoryGuids.Bluetooth, 2);
				public static readonly UInt32Property DeviceFlags = new(ShellPropertyCategoryGuids.Bluetooth, 3);
				public static readonly StringProperty DeviceManufacturer = new(ShellPropertyCategoryGuids.Bluetooth, 4);
				public static readonly StringProperty DeviceModelNumber = new(ShellPropertyCategoryGuids.Bluetooth, 5);
				public static readonly ByteProperty DeviceVIDSource = new(ShellPropertyCategoryGuids.Bluetooth, 6);
				public static readonly UInt16Property DeviceVID = new(ShellPropertyCategoryGuids.Bluetooth, 7);
				public static readonly UInt16Property DevicePID = new(ShellPropertyCategoryGuids.Bluetooth, 8);
				public static readonly UInt16Property DeviceProductVersion = new(ShellPropertyCategoryGuids.Bluetooth, 9);
				public static readonly UInt32Property ClassOfDevice = new(ShellPropertyCategoryGuids.Bluetooth, 10);
				public static readonly FileTimeProperty LastConnectedTime = new(ShellPropertyCategoryGuids.Bluetooth, 11);
			}

			public static class Hid
			{
				public static readonly UInt16Property UsagePage = new(DeviceInterfaceClassGuids.Hid, 2);
				public static readonly UInt16Property UsageId = new(DeviceInterfaceClassGuids.Hid, 3);
				public static readonly BooleanProperty IsReadOnly = new(DeviceInterfaceClassGuids.Hid, 4);
				public static readonly UInt16Property VendorId = new(DeviceInterfaceClassGuids.Hid, 5);
				public static readonly UInt16Property ProductId = new(DeviceInterfaceClassGuids.Hid, 6);
				public static readonly UInt16Property VersionNumber = new(DeviceInterfaceClassGuids.Hid, 7);
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
				public static readonly StringListProperty DeviceInterfaceClasses = new(ShellPropertyCategoryGuids.WinUsb, 7);
				public static readonly ByteProperty UsbClass = new(ShellPropertyCategoryGuids.WinUsb, 4);
				public static readonly UInt16Property UsbProductId = new(ShellPropertyCategoryGuids.WinUsb, 3);
				public static readonly ByteProperty UsbProtocol = new(ShellPropertyCategoryGuids.WinUsb, 6);
				public static readonly ByteProperty UsbSubClass = new(ShellPropertyCategoryGuids.WinUsb, 5);
				public static readonly UInt16Property UsbVendorId = new(ShellPropertyCategoryGuids.WinUsb, 2);
			}
		}

		public static class Devices
		{
			public static class Aep
			{
			}

			public static readonly GuidProperty ContainerId = new(ShellPropertyCategoryGuids.DeviceContainerMapping, 2);
			public static readonly GuidProperty InterfaceClassGuid = new(ShellPropertyCategoryGuids.DeviceInterface, 4);
			public static readonly StringProperty DeviceInstanceId = new(ShellPropertyCategoryGuids.DeviceContainer, 256);
			public static readonly BooleanProperty InterfaceEnabled = new(ShellPropertyCategoryGuids.DeviceInterface, 3);

			public static readonly StringProperty DeviceDesc = new(ShellPropertyCategoryGuids.Device, 2);
			public static readonly StringListProperty HardwareIds = new(ShellPropertyCategoryGuids.Device, 3);
			public static readonly StringListProperty CompatibleIds = new(ShellPropertyCategoryGuids.Device, 4);
			public static readonly StringProperty Service = new(ShellPropertyCategoryGuids.Device, 6);
			public static readonly StringProperty Class = new(ShellPropertyCategoryGuids.Device, 9);
			public static readonly GuidProperty ClassGuid = new(ShellPropertyCategoryGuids.Device, 10);
			public static readonly StringProperty Driver = new(ShellPropertyCategoryGuids.Device, 11);
			public static readonly UInt32Property ConfigFlags = new(ShellPropertyCategoryGuids.Device, 12);
			public static readonly StringProperty Manufacturer = new(ShellPropertyCategoryGuids.Device, 13);
			public static readonly StringProperty FriendlyName = new(ShellPropertyCategoryGuids.Device, 14);
			public static readonly StringProperty LocationInfo = new(ShellPropertyCategoryGuids.Device, 15);
			public static readonly StringProperty PhysicalDeviceObjectName = new(ShellPropertyCategoryGuids.Device, 16);
			public static readonly UInt32Property Capabilities = new(ShellPropertyCategoryGuids.Device, 17);
			public static readonly UInt32Property UiNumber = new(ShellPropertyCategoryGuids.Device, 18);
			public static readonly StringListProperty UpperFilters = new(ShellPropertyCategoryGuids.Device, 19);
			public static readonly StringListProperty LowerFilters = new(ShellPropertyCategoryGuids.Device, 20);
			public static readonly GuidProperty BusTypeGuid = new(ShellPropertyCategoryGuids.Device, 21);
			public static readonly UInt32Property LegacyBusType = new(ShellPropertyCategoryGuids.Device, 22);
			public static readonly UInt32Property BusNumber = new(ShellPropertyCategoryGuids.Device, 23);
			public static readonly StringProperty EnumeratorName = new(ShellPropertyCategoryGuids.Device, 24);
			//public static readonly PropertyKey Security = new(ShellPropertyCategoryGuids.Device, 25);
			//public static readonly PropertyKey SecurityDescriptorString = new(ShellPropertyCategoryGuids.Device, 26);
			public static readonly UInt32Property DeviceType = new(ShellPropertyCategoryGuids.Device, 27);
			public static readonly BooleanProperty Exclusive = new(ShellPropertyCategoryGuids.Device, 28);
			public static readonly UInt32Property Characteristics = new(ShellPropertyCategoryGuids.Device, 29);
			public static readonly UInt32Property Address = new(ShellPropertyCategoryGuids.Device, 30);
			public static readonly StringProperty UiNumberDescFormat = new(ShellPropertyCategoryGuids.Device, 31);
			public static readonly BinaryProperty PowerData = new(ShellPropertyCategoryGuids.Device, 32);
			public static readonly UInt32Property RemovalPolicy = new(ShellPropertyCategoryGuids.Device, 33);
			public static readonly UInt32Property RemovalPolicyDefault = new(ShellPropertyCategoryGuids.Device, 34);
			public static readonly UInt32Property RemovalPolicyOverride = new(ShellPropertyCategoryGuids.Device, 35);
			public static readonly UInt32Property InstallState = new(ShellPropertyCategoryGuids.Device, 36);
			public static readonly StringListProperty LocationPaths = new(ShellPropertyCategoryGuids.Device, 37);
			public static readonly GuidProperty BaseContainerId = new(ShellPropertyCategoryGuids.Device, 38);

			public static readonly UInt32Property DevObjectType = new(new(0x13673f42, 0xa3d6, 0x49f6, 0xb4, 0xda, 0xae, 0x46, 0xe0, 0xc5, 0x23, 0x7c), 2);
		}

		public static readonly StringProperty ItemNameDisplay = new(ShellPropertyCategoryGuids.Storage, 10);
	}
}
