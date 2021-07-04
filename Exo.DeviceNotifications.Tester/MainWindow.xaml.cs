using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Exo.Core.Services;

namespace Exo.DeviceNotifications.Tester
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, IDeviceNotificationSink
	{
		private readonly NotificationWindow _notificationWindow;
		private readonly IDisposable _notificationRegistration;

		public MainWindow()
		{
			InitializeComponent();
			_notificationWindow = new NotificationWindow();
			_notificationRegistration = _notificationWindow.RegisterDeviceNotifications(this);
		}

		private GlobalConfigurationViewModel ViewModel => (GlobalConfigurationViewModel)DataContext;

		private void OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
			=> ViewModel.Devices.Add(new DeviceViewModel(DeviceInterfaceClassViewModel.Get(deviceInterfaceClassGuid), deviceName));

		private void OnDeviceQueryRemove(Guid deviceInterfaceClassGuid, string deviceName)
		{
		}

		private void OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName)
		{
		}

		private void OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
		{
		}

		private void OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
		{
			var devices = ViewModel.Devices;
			for (int i = 0; i < devices.Count; i++)
			{
				var device = devices[i];
				if (device.DeviceInterfaceClass.Guid == deviceInterfaceClassGuid && device.DeviceName == deviceName)
				{
					devices.RemoveAt(i);
					return;
				}
			}
		}

		void IDeviceNotificationSink.OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
			=> Dispatcher.InvokeAsync(() => OnDeviceArrival(deviceInterfaceClassGuid, deviceName));

		bool IDeviceNotificationSink.OnDeviceQueryRemove(Guid deviceInterfaceClassGuid, string deviceName)
		{
			Dispatcher.InvokeAsync(() => OnDeviceQueryRemove(deviceInterfaceClassGuid, deviceName));
			return false;
		}

		void IDeviceNotificationSink.OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName)
			=> Dispatcher.InvokeAsync(() => OnDeviceQueryRemoveFailed(deviceInterfaceClassGuid, deviceName));

		void IDeviceNotificationSink.OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
			=> Dispatcher.InvokeAsync(() => OnDeviceRemovePending(deviceInterfaceClassGuid, deviceName));

		void IDeviceNotificationSink.OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
			=> Dispatcher.InvokeAsync(() => OnDeviceRemoveComplete(deviceInterfaceClassGuid, deviceName));
	}
}
