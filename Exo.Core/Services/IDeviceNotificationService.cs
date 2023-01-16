using System;
using Microsoft.Win32.SafeHandles;

namespace Exo.Services
{
	public interface IDeviceNotificationService
	{
		/// <summary>Registers to receive device notifications for the specified device file handle.</summary>
		/// <param name="deviceFileHandle">An open file handle for the device.</param>
		/// <param name="state">An user-provided state object.</param>
		/// <param name="sink">The sink that will receive notifications.</param>
		IDisposable RegisterDeviceNotifications<T>(SafeFileHandle deviceFileHandle, T state, IDeviceHandleNotificationSink<T> sink);

		/// <summary>Registers to receive device notifications for the specified device interface class.</summary>
		/// <param name="deviceInterfaceClassGuid">The GUID of the device interface class for which notifications are requested.</param>
		/// <param name="sink">The sink that will receive notifications.</param>
		IDisposable RegisterDeviceNotifications(Guid deviceInterfaceClassGuid, IDeviceNotificationSink sink);

		/// <summary>Registers to receive device notifications for all device interface classes.</summary>
		/// <param name="sink">The sink that will receive notifications.</param>
		IDisposable RegisterDeviceNotifications(IDeviceNotificationSink sink);
	}
}
