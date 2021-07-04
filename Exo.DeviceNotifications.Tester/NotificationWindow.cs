using System;
using System.Windows.Interop;
using Exo.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace Exo.DeviceNotifications.Tester
{
	internal sealed class NotificationWindow : IDeviceNotificationService, IDisposable
	{
		private const int WmDeviceChange = 0x0219;

		private readonly HwndSource _hwndSource;
		private readonly DeviceNotificationEngine _deviceNotificationEngine;

		public NotificationWindow()
		{
			_hwndSource = new HwndSource(new HwndSourceParameters("Device Notification Window", 0, 0) { HwndSourceHook = OnWindowMessage, WindowClassStyle = 0, WindowStyle = 0, ExtendedWindowStyle = 0 });
			_deviceNotificationEngine = DeviceNotificationEngine.CreateForWindow(_hwndSource.Handle);
		}

		public void Dispose()
		{
			_deviceNotificationEngine.Dispose();
			_hwndSource.Dispose();
		}

		private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == WmDeviceChange)
			{
				handled = true;
				return (IntPtr)_deviceNotificationEngine.HandleNotification((int)wParam, lParam);
			}
			handled = false;
			return IntPtr.Zero;
		}

		public IDisposable RegisterDeviceNotifications<T>(SafeFileHandle deviceFileHandle, T state, IDeviceHandleNotificationSink<T> sink)
			=> _deviceNotificationEngine.RegisterDeviceNotifications(deviceFileHandle, state, sink);

		public IDisposable RegisterDeviceNotifications(Guid deviceInterfaceClassGuid, IDeviceNotificationSink sink)
			=> _deviceNotificationEngine.RegisterDeviceNotifications(deviceInterfaceClassGuid, sink);

		public IDisposable RegisterDeviceNotifications(IDeviceNotificationSink sink)
			=> _deviceNotificationEngine.RegisterDeviceNotifications(sink);
	}
}
