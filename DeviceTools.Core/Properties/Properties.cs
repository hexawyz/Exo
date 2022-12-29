using DeviceTools.FilterExpressions;
using static DeviceTools.NativeMethods;

namespace DeviceTools.Properties
{
	public static class System
	{
		public static class Device
		{
		}

		public static class DeviceInterface
		{
			public static class Hid
			{
				public static readonly UInt16Property UsagePage = new(DeviceInterfaceClassGuids.Hid, 2);
				public static readonly UInt16Property UsageId = new(DeviceInterfaceClassGuids.Hid, 3);
				public static readonly BooleanProperty IsReadOnly = new(DeviceInterfaceClassGuids.Hid, 4);
				public static readonly UInt16Property VendorId = new(DeviceInterfaceClassGuids.Hid, 5);
				public static readonly UInt16Property ProductId = new(DeviceInterfaceClassGuids.Hid, 6);
				public static readonly UInt16Property VersionNumber = new(DeviceInterfaceClassGuids.Hid, 7);
			}
		}

		public static class Devices
		{
			public static readonly GuidProperty ContainerId = new(ShellPropertyCategoryGuids.DeviceContainerMapping, 2);
			public static readonly GuidProperty InterfaceClassGuid = new(ShellPropertyCategoryGuids.DeviceInterface, 4);
			public static readonly StringProperty DeviceInstanceId = new(ShellPropertyCategoryGuids.DeviceContainer, 256);
			public static readonly BooleanProperty InterfaceEnabled = new(ShellPropertyCategoryGuids.DeviceInterface, 3);

			public static readonly GuidProperty ClassGuid = new(ShellPropertyCategoryGuids.Device, 10);
			public static readonly StringListProperty HardwareIds = new(ShellPropertyCategoryGuids.Device, 3);

		}

		public static readonly StringProperty ItemNameDisplay = new(ShellPropertyCategoryGuids.Storage, 10);
	}
}
