using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using static Exo.DeviceNotifications.NativeMethods;

namespace Exo.DeviceNotifications
{
	public sealed class DeviceNotificationEngine
	{
		private ConcurrentDictionary<IntPtr, DeviceFileNotificationRegistration>? _fileHandleToRegistrationMappings; // Keep a mapping from device file HANDLE values to .net registration objects.

		public unsafe int HandleNotification(int eventType, IntPtr eventData)
		{
			switch (((DeviceBroadcastHeader*)eventData)->DeviceType)
			{
			case BroadcastDeviceType.DeviceInterface:
				var broadcastDevInterface = (DeviceBrodcastDeviceInterface*)eventData;
				string? deviceName = null;

				if (broadcastDevInterface->Size > sizeof(DeviceBrodcastDeviceInterface))
				{
					int extraLength = broadcastDevInterface->Size - sizeof(DeviceBrodcastDeviceInterface);

					var nameSpan = new ReadOnlySpan<char>((byte*)eventData + sizeof(DeviceBrodcastDeviceInterface), extraLength);
					int endIndex = nameSpan.IndexOf('\0');
					if (endIndex >= 0)
					{
						nameSpan = nameSpan.Slice(0, endIndex);
					}
					deviceName = nameSpan.ToString();
				}

				if (eventType == (int)NativeMethods.DeviceBroadcastType.DeviceQueryRemove)
				{
					//return OnDeviceQueryRemove(broadcastDevInterface->ClassGuid, deviceName)
					//	? 0
					//	: BROADCAST_QUERY_DENY;
					return 0;
				}
				else
				{
					return 0;
					//ThreadPool.QueueUserWorkItem(_ => DefferedDeviceEvent((DeviceBroadcastType)eventType, broadcastDevInterface->dbcc_classguid, deviceName));
				}
				break;
			case BroadcastDeviceType.Handle:
				var broadcastHandle = (DeviceBroadcastHandle*)eventData;

				if (_fileHandleToRegistrationMappings is not null && _fileHandleToRegistrationMappings.TryGetValue(broadcastHandle->dbch_handle, out DeviceFileNotificationRegistration? registration))
				{
					if (eventType == DBT_DEVICEQUERYREMOVE)
					{
						return OnDeviceQueryRemove(registration.DeviceFileHandle, registration.UserToken)
							? 0
							: BROADCAST_QUERY_DENY;
					}
					else
					{
						// TODO: Handle extra data for device custom events
						ThreadPool.QueueUserWorkItem(_ => DefferedDeviceEvent((DeviceBroadcastType)eventType, registration.DeviceFileHandle, registration.UserToken));
					}
				}
				break;
			default:
				break;
			}
			return 0;
		}
		/// <summary>
		/// Register to receive device notifications for the specified file handle.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public unsafe IDisposable RegisterDeviceNotifications(SafeFileHandle handle, object? userToken)
		{
			return DeviceFileNotificationRegistration.RegisterDeviceNotifications(
				LazyInitializer.EnsureInitialized(ref _fileHandleToRegistrationMappings),
				ServiceHandle,
				handle,
				userToken);
		}

		/// <summary>
		/// Register to receive device notifications for the specified device interface class.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public IDisposable RegisterDeviceNotifications(Guid interfaceClassGuid) => DeviceInterfaceClassNotificationRegistration.RegisterDeviceNotifications(ServiceHandle, interfaceClassGuid);

		/// <summary>
		/// Register to receive device notifications for all device interface classes.
		/// </summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public IDisposable RegisterDeviceNotifications() => DeviceInterfaceClassNotificationRegistration.RegisterDeviceNotifications(ServiceHandle);

		// Class used to manage handle-based device registrations.
		// It would be possible to shove everything within SafeDeviceNotificationHandle to save one object allocation, but this feels a bit cleaner.
		internal sealed class DeviceFileNotificationRegistration : IDisposable
		{
			public static unsafe DeviceFileNotificationRegistration RegisterDeviceNotifications(ConcurrentDictionary<IntPtr, DeviceFileNotificationRegistration> registrationDictionary, bool isServiceHandle, IntPtr handle, SafeFileHandle fileHandle, object? userToken)
			{
				var registration = new DeviceFileNotificationRegistration(registrationDictionary, fileHandle, userToken);

				bool success = false;
				// Prevent the device file handle from being released while it is being used for notifications.
				fileHandle.DangerousAddRef(ref success);

				if (!success)
				{
					throw new InvalidOperationException(SR.InvalidDeviceFileHandle);
				}

				var rawFileHandle = fileHandle.DangerousGetHandle();

				// Register the current (yet not fully initialized) instance so that notifications can provide the correct UserToken.
				registrationDictionary[rawFileHandle] = registration;

				try
				{
					var notificationHandle = RegisterDeviceNotificationW(
						serviceHandle,
						new DeviceBroadcastHandle
						{
							Size = sizeof(DeviceBroadcastHandle),
							DeviceType = BroadcastDeviceType.Handle,
							DeviceHandle = rawFileHandle,
						},
						isServiceHandle ? DeviceNotificationFlags.ServiceHandle : DeviceNotificationFlags.WindowHandle);
					if (notificationHandle.IsInvalid)
					{
						throw new Win32Exception(Marshal.GetLastWin32Error());
					}
					registration._deviceNotificationHandle = notificationHandle;

					return registration;
				}
				catch
				{
					registration.Dispose();
					throw;
				}
			}

			private readonly ConcurrentDictionary<IntPtr, DeviceFileNotificationRegistration> _registrationDictionary;
			private SafeDeviceNotificationHandle? _deviceNotificationHandle;
			public SafeFileHandle DeviceFileHandle { get; }
			private int _isDisposed;

			/// <summary>
			/// A user-supplied object used to track this instance.
			/// </summary>
			public object? UserToken { get; }

			private DeviceFileNotificationRegistration(ConcurrentDictionary<IntPtr, DeviceFileNotificationRegistration> registrationDictionary, SafeFileHandle fileHandle, object? userToken)
			{
				_registrationDictionary = registrationDictionary;
				DeviceFileHandle = fileHandle;
				UserToken = userToken;
			}

			~DeviceFileNotificationRegistration() => Dispose(false);

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing)
			{
				if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 0)
				{
					// Whenever possible, this instance should be removed from the mapping dictionary to avoid a permanent leak.
					_registrationDictionary?.TryRemove(DeviceFileHandle?.DangerousGetHandle() ?? default, out _);

					if (disposing)
					{
						_deviceNotificationHandle?.Dispose();
					}
				}
			}
		}

		// Class used to manage device interface class notification registrations.
		// These are easier to manage, as they are bound to an easily comaprable GUID and not a HANDLE.
		internal sealed class DeviceInterfaceClassNotificationRegistration : IDisposable
		{
			public static unsafe DeviceInterfaceClassNotificationRegistration RegisterDeviceNotifications(IntPtr serviceHandle, Guid interfaceClassGuid)
			{
				var handle = RegisterDeviceNotificationW(
					serviceHandle,
					new DeviceBrodcastDeviceInterface
					{
						Size = sizeof(DeviceBrodcastDeviceInterface),
						DeviceType = BroadcastDeviceType.DeviceInterface,
						ClassGuid = interfaceClassGuid,
					},
					DeviceNotificationFlags.ServiceHandle);
				if (handle.IsInvalid)
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				return new DeviceInterfaceClassNotificationRegistration(handle);
			}

			public static unsafe DeviceInterfaceClassNotificationRegistration RegisterDeviceNotifications(IntPtr serviceHandle)
			{
				var handle = RegisterDeviceNotificationW(
					serviceHandle,
					new DeviceBrodcastDeviceInterface
					{
						Size = sizeof(DeviceBrodcastDeviceInterface),
						DeviceType = BroadcastDeviceType.DeviceInterface,
					},
					DeviceNotificationFlags.ServiceHandle | DeviceNotificationFlags.AllInterfaceClasses);
				if (handle.IsInvalid)
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
				return new DeviceInterfaceClassNotificationRegistration(handle);
			}

			private readonly SafeDeviceNotificationHandle _safeDeviceNotificationHandle;

			private DeviceInterfaceClassNotificationRegistration(SafeDeviceNotificationHandle safeDeviceNotificationHandle) => _safeDeviceNotificationHandle = safeDeviceNotificationHandle;

			public void Dispose() => _safeDeviceNotificationHandle.Dispose();
		}
	}
}
}
