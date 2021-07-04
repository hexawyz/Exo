namespace Exo.DeviceNotifications.Tester
{
	internal sealed class DeviceViewModel : BindableObject
	{
		public DeviceInterfaceClassViewModel DeviceInterfaceClass { get; }
		public string DeviceName { get; }

		public DeviceViewModel(DeviceInterfaceClassViewModel deviceInterfaceClass, string deviceName)
		{
			DeviceInterfaceClass = deviceInterfaceClass;
			DeviceName = deviceName;
		}
	}
}
