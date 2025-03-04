using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Exo.Services;
using Microsoft.Win32.SafeHandles;
using static Exo.DeviceNotifications.NativeMethods;

namespace Exo.DeviceNotifications
{
	/// <summary>Manage device notifications for a window or service.</summary>
	/// <remarks>
	/// <para>This class exposes features the device notification API in a more object oriented way.</para>
	/// <para>Methods of this class are designed to be safe when called concurrently, given the unpredictable timing of device notifications.</para>
	/// <para>
	/// It is a tool for registering notifications and processing device notifications received from the same service or window.
	/// A separate piece of code must be used to forward the device notifications from the window procedure or the service control handler.
	/// </para>
	/// </remarks>
	public sealed class DeviceNotificationEngine : IDeviceNotificationService, IDisposable
	{
		// Keep mappings from raw handle values to the device notification object.
		// This is required for two reasons:
		//  1 - Device (handle) notification events can only be identified by their handle(s), and we cannot recreate a new SafeHandle every time (that would be quite bad)
		//  2 - There is a race condition when registering (handle) notifications, as notifications can be triggered *before* we get our hands on the actual handle
		// Notifications will be properly matched by first binding the (raw) device file handle values to the appropriate disposable registration object,
		// and then, either immediately after registration or upon first registration, whichever comes first, creating the binding from the device notification handle
		// to the same disposable registration object. *After* the mapping from HDEVNOTIFY to object is effective, we don't need the mapping from HANDLE anymore.
		private readonly ConcurrentDictionary<IntPtr, WeakReference<DeviceFileNotificationRegistration>> _fileHandleToRegistrationMappings = new(); // HANDLE to registration object
		private readonly ConcurrentDictionary<IntPtr, WeakReference<DeviceFileNotificationRegistration>> _deviceNotificationHandleToRegistrationMappings = new(); // HDEVNOTIFY to registration object

		// Device interface classes look a bit simpler buf there is some work to be done to
		//  1 - Track all notification sinks and allow each one to be detached independently
		//  2 - Avoid duplicating native registrations for the same scope
		// Optionally, we could merge global notifications with per-class notifications depending on the active set of registrations, but that might be too much (complex) work.
		// Also, global notifications might not be all that useful. (We may mostly want to listen for a few select class GUIDs, such as HID or Monitor)
		private DeviceInterfaceClassSharedNotificationRegistration? _globalDeviceInterfaceClassRegistration;
		private readonly ConcurrentDictionary<Guid, DeviceInterfaceClassSharedNotificationRegistration> _deviceInterfaceClassNotificationRegistrations = new();

		private readonly IntPtr _targetHandle;
		private readonly bool _isServiceHandle;

		public static DeviceNotificationEngine CreateForWindow(IntPtr handle)
			=> new DeviceNotificationEngine(handle, false);

		public static DeviceNotificationEngine CreateForService(IntPtr handle)
			=> new DeviceNotificationEngine(handle, true);

		private static bool SafeRemove<TKey, TValue>(ConcurrentDictionary<TKey, TValue>? dictionary, TKey key, TValue value) where TKey : notnull
			=> ((IDictionary<TKey, TValue>?)dictionary)?.Remove(new KeyValuePair<TKey, TValue>(key, value)) ?? false;

		private DeviceNotificationEngine(IntPtr targetHandle, bool isServiceHandle)
		{
			_targetHandle = targetHandle;
			_isServiceHandle = isServiceHandle;
		}

		public void Dispose()
		{
			// TODO: Unregisters for all notifications and prevent further registration for notifications.
		}

		/// <summary>Registers to receive device notifications for the specified device file handle.</summary>
		/// <param name="deviceFileHandle">An open file handle for the device.</param>
		/// <param name="state">An user-provided state object.</param>
		/// <param name="sink">The sink that will receive notifications.</param>
		public unsafe IDisposable RegisterDeviceNotifications<T>(SafeFileHandle deviceFileHandle, T state, IDeviceHandleNotificationSink<T> sink)
			=> DeviceFileNotificationRegistration.RegisterDeviceNotifications(this, _targetHandle, _isServiceHandle, deviceFileHandle, state, sink);

		/// <summary>Registers to receive device notifications for the specified device interface class.</summary>
		/// <param name="deviceInterfaceClassGuid">The GUID of the device interface class for which notifications are requested.</param>
		/// <param name="sink">The sink that will receive notifications.</param>
		public IDisposable RegisterDeviceNotifications(Guid deviceInterfaceClassGuid, IDeviceNotificationSink sink)
		{
			while (true)
			{
				var sharedRegistration = _deviceInterfaceClassNotificationRegistrations.GetOrAdd(deviceInterfaceClassGuid, guid => new DeviceInterfaceClassSharedNotificationRegistration(this, guid));

				if (sharedRegistration.TryRegister(sink, out var registration))
				{
					return registration;
				}
			}
		}

		/// <summary>Registers to receive device notifications for all device interface classes.</summary>
		/// <summary>Registers to receive device notifications for all device interface classes.</summary>
		/// <param name="sink">The sink that will receive notifications.</param>
		public IDisposable RegisterDeviceNotifications(IDeviceNotificationSink sink)
		{
			DeviceInterfaceClassSharedNotificationRegistration? allocated = null;

			while (true)
			{
				var sharedRegistration = Volatile.Read(ref _globalDeviceInterfaceClassRegistration);

				if (sharedRegistration is null)
				{
					sharedRegistration = Interlocked.CompareExchange(ref _globalDeviceInterfaceClassRegistration, allocated ??= new DeviceInterfaceClassSharedNotificationRegistration(this), null) ?? allocated;
				}

				if (sharedRegistration.TryRegister(sink, out var registration))
				{
					return registration;
				}
				else if (sharedRegistration == allocated)
				{
					allocated = null;
				}
			}
		}

		/// <summary>Entry point of device notifications handling.</summary>
		/// <remarks>
		/// <para>The window or service that is the notification target of this instance should call this method with the data they received for device notifications.</para>
		/// <para>Windows should intercept the WM_DEVICECHANGE (<c>0x0219</c>) message and forward the <c>lParam</c> and <c>wParam</c> parameters.</para>
		/// <para>Services should receive SERVICE_CONTROL_DEVICEEVENT messages and forward the <c>dwEventType</c> and <c>lpEventData</c> parameters.</para>
		/// </remarks>
		/// <param name="eventType">The raw event type.</param>
		/// <param name="eventData">The raw event data.</param>
		/// <returns>This method returns <c>0</c> if messages were processed correctly, or a <c>BROADCAST_QUERY_DENY</c> if the event was a device removal query that was denied by one of the registered sinks.</returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public unsafe int HandleNotification(int eventType, IntPtr eventData)
		{
			// Filter events that this code does not handle. (e.g. DBT_DEVNODES_CHANGED, DBT_QUERYCHANGECONFIG, DBT_CONFIGCHANGED, DBT_CONFIGCHANGECANCELED)
			if (eventType < 0x8000 || eventType > 0x8006 && eventType != 0xFFFF) return 0;

			switch (((DeviceBroadcastHeader*)eventData)->DeviceType)
			{
			case BroadcastDeviceType.DeviceInterface:
				var broadcastDevInterface = (DeviceBrodcastDeviceInterface*)eventData;
				string deviceName = string.Empty;

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

				return (_globalDeviceInterfaceClassRegistration is null || DispatchNotification((DeviceBroadcastType)eventType, _globalDeviceInterfaceClassRegistration, broadcastDevInterface->ClassGuid, deviceName))
					& (!_deviceInterfaceClassNotificationRegistrations.TryGetValue(broadcastDevInterface->ClassGuid, out var deviceInterfaceRegistration) || DispatchNotification((DeviceBroadcastType)eventType, deviceInterfaceRegistration, broadcastDevInterface->ClassGuid, deviceName)) ?
					0 :
					BROADCAST_QUERY_DENY;
			case BroadcastDeviceType.Handle:
				var broadcastHandle = (DeviceBroadcastHandle*)eventData;

				return GetNotificationRegistration(broadcastHandle->DeviceNotifyHandle, broadcastHandle->DeviceHandle) is not null and var deviceRegistration
					&& DispatchNotification((DeviceBroadcastType)eventType, deviceRegistration) ?
						0 :
						BROADCAST_QUERY_DENY;
			default:
				break;
			}
			return 0;
		}

		private DeviceFileNotificationRegistration? GetNotificationRegistration(IntPtr deviceNotificationHandle, IntPtr deviceFileHandle)
		{
			DeviceFileNotificationRegistration? registration = null;
			bool canRetry = false;

			// Operations are done sequentially to avoid closure allocations and to prevent race conditions.
			while (true)
			{
				// TODO: See if we can somehow reuse weak references. (And then we may not be able to remove them blindly from the dictionaries)
				if (_deviceNotificationHandleToRegistrationMappings.TryGetValue(deviceNotificationHandle, out var weakReference))
				{
					if (weakReference.TryGetTarget(out registration))
					{
						break;
					}
					else
					{
						// These dictionary entries should already be cleaned up by the finalizers, but it shouldn't hurt too much doing it here too.
						SafeRemove(_deviceNotificationHandleToRegistrationMappings, deviceNotificationHandle, weakReference);
						SafeRemove(_fileHandleToRegistrationMappings, deviceFileHandle, weakReference);
					}
					canRetry = true;
				}

				// We don't expect to find an empty weak reference here, as the code above would have removed it,
				// and a new entry would have a valid reference held by the RegisterDeviceNotifications method.
				if (_fileHandleToRegistrationMappings.TryGetValue(deviceFileHandle, out weakReference) && weakReference.TryGetTarget(out registration))
				{
					registration.TrySetHandle(deviceNotificationHandle);
					break;
				}
				else if (!canRetry)
				{
					break;
				}
				else
				{
					// Try getting the registration object again, since absence from the HANDLE => object mappings should indicate that the correct mapping has been established.
					canRetry = false;
				}
			}
			// Will return null if the registration was not found at all.
			// This should only occur when unregistering the notifications because code is executed concurrently.
			return registration;
		}

		private static bool DispatchNotification(DeviceBroadcastType eventType, DeviceInterfaceClassSharedNotificationRegistration registration, Guid deviceInterfaceClassGuid, string deviceName)
		{
			switch (eventType)
			{
			case DeviceBroadcastType.DeviceArrival:
				registration.OnDeviceArrival(deviceInterfaceClassGuid, deviceName);
				break;
			case DeviceBroadcastType.DeviceQueryRemove:
				return registration.OnDeviceQueryRemove(deviceInterfaceClassGuid, deviceName);
			case DeviceBroadcastType.DeviceQueryRemoveFailed:
				registration.OnDeviceQueryRemoveFailed(deviceInterfaceClassGuid, deviceName);
				break;
			case DeviceBroadcastType.DeviceRemovePending:
				registration.OnDeviceRemovePending(deviceInterfaceClassGuid, deviceName);
				break;
			case DeviceBroadcastType.DeviceRemoveComplete:
				registration.OnDeviceRemoveComplete(deviceInterfaceClassGuid, deviceName);
				break;
			}
			return true;
		}

		private static bool DispatchNotification(DeviceBroadcastType eventType, DeviceFileNotificationRegistration registration)
		{
			switch (eventType)
			{
			case DeviceBroadcastType.DeviceArrival:
				registration.OnDeviceArrival();
				break;
			case DeviceBroadcastType.DeviceQueryRemove:
				return registration.OnDeviceQueryRemove();
			case DeviceBroadcastType.DeviceQueryRemoveFailed:
				registration.OnDeviceQueryRemoveFailed();
				break;
			case DeviceBroadcastType.DeviceRemovePending:
				registration.OnDeviceRemovePending();
				break;
			case DeviceBroadcastType.DeviceRemoveComplete:
				registration.OnDeviceRemoveComplete();
				break;
			}
			return true;
		}

		private static unsafe SafeDeviceNotificationHandle RegisterDeviceNotification(IntPtr handle, bool isServiceHandle, Guid? interfaceClassGuid)
		{
			var notificationFilter = new DeviceBrodcastDeviceInterface
			{
				Size = sizeof(DeviceBrodcastDeviceInterface),
				DeviceType = BroadcastDeviceType.DeviceInterface,
			};

			var flags = isServiceHandle ? DeviceNotificationFlags.ServiceHandle : DeviceNotificationFlags.WindowHandle;

			if (interfaceClassGuid != null)
			{
				notificationFilter.ClassGuid = interfaceClassGuid.GetValueOrDefault();
			}
			else
			{
				flags |= DeviceNotificationFlags.AllInterfaceClasses;
			}

			var notificationHandle = RegisterDeviceNotificationW(handle, notificationFilter, flags);

			if (notificationHandle == IntPtr.Zero)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return new SafeDeviceNotificationHandle(notificationHandle);
		}

		private abstract class DeviceNotificationRegistrationBase : IDisposable
		{
			public abstract bool IsDisposed { get; }

			~DeviceNotificationRegistrationBase() => Dispose(false);

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected abstract void Dispose(bool disposing);
		}

		// Class used to manage handle-based device registrations.
		// It would be possible to shove everything within SafeDeviceNotificationHandle to save one object allocation, but this feels a bit cleaner.
		private abstract class DeviceFileNotificationRegistration : DeviceNotificationRegistrationBase
		{
			public static unsafe DeviceFileNotificationRegistration<T> RegisterDeviceNotifications<T>(DeviceNotificationEngine engine, IntPtr handle, bool isServiceHandle, SafeFileHandle fileHandle, T userToken, IDeviceHandleNotificationSink<T> sink)
			{
				var registration = new DeviceFileNotificationRegistration<T>(engine, fileHandle, userToken, sink);

				try
				{
					var rawFileHandle = fileHandle.DangerousGetHandle();

					// Register the current (yet not fully initialized) instance so that notifications can provide the correct UserToken.
					if (!engine._fileHandleToRegistrationMappings.TryAdd(rawFileHandle, registration._weakReference))
					{
						throw new InvalidOperationException("Notifications for the specified device were already enabled.");
					}

					var notificationHandle = RegisterDeviceNotificationW
					(
						handle,
						new DeviceBroadcastHandle
						{
							Size = sizeof(DeviceBroadcastHandle),
							DeviceType = BroadcastDeviceType.Handle,
							DeviceHandle = rawFileHandle,
						},
						isServiceHandle ? DeviceNotificationFlags.ServiceHandle : DeviceNotificationFlags.WindowHandle
					);

					if (notificationHandle == IntPtr.Zero)
					{
						throw new Win32Exception(Marshal.GetLastWin32Error());
					}

					registration.TrySetHandle(notificationHandle);

					return registration;
				}
				catch
				{
					registration.Dispose();
					throw;
				}
			}

			private const int StateUninitialized = 0;
			private const int StateInitializing = 1;
			private const int StateInitialized = 2;
			private const int StateDisposed = 3;

			private readonly DeviceNotificationEngine _engine;
			private SafeDeviceNotificationHandle? _deviceNotificationHandle;
			public SafeFileHandle DeviceFileHandle { get; }
			private readonly WeakReference<DeviceFileNotificationRegistration> _weakReference;
			private int _state;

			public override bool IsDisposed => Volatile.Read(ref _state) == StateDisposed;

			protected DeviceFileNotificationRegistration(DeviceNotificationEngine engine, SafeFileHandle fileHandle)
			{
				bool success = false;
				// Prevent the device file handle from being released while it is being used for notifications.
				fileHandle.DangerousAddRef(ref success);

				if (!success)
				{
					throw new InvalidOperationException("The specified device file handle is invalid.");
				}

				_engine = engine;
				DeviceFileHandle = fileHandle;
				_weakReference = new WeakReference<DeviceFileNotificationRegistration>(this);
			}

			protected override void Dispose(bool disposing)
			{
				var state = Volatile.Read(ref _state);
				while (true)
				{
					if (state == StateDisposed) return;
					if (state != (state = Interlocked.CompareExchange(ref _state, StateDisposed, state))) continue;

					// Remove dictionary entries that were related to this instance in order to avoid a permanent leak.
					if (_engine is not null)
					{
						if (_weakReference is not null)
						{
							if (DeviceFileHandle is not null)
							{
								SafeRemove(_engine._fileHandleToRegistrationMappings, DeviceFileHandle.DangerousGetHandle(), _weakReference);
							}
							if (_deviceNotificationHandle is not null)
							{
								SafeRemove(_engine._deviceNotificationHandleToRegistrationMappings, _deviceNotificationHandle.DangerousGetHandle(), _weakReference);
							}
						}
					}

					DeviceFileHandle?.DangerousRelease();

					if (disposing)
					{
						_deviceNotificationHandle?.Dispose();
					}
				}
			}

			// Initialization is done once, either in RegisterDeviceNotifications or in GetNotificationRegistration.
			// NB: User code should *never* see registrations that are not fully initialized.
			// This is required so that they are not disposed before they are fully initialized, and do not leak objects.
			internal void TrySetHandle(IntPtr deviceNotificationHandle)
			{
				// Guarantees that initialization is only ever done once *and* if the instance has not been disposed when the method starts.
				if (StateUninitialized != Interlocked.CompareExchange(ref _state, StateInitializing, StateUninitialized)) return;

				if (_engine._deviceNotificationHandleToRegistrationMappings.TryAdd(deviceNotificationHandle, _weakReference))
				{
					var handle = new SafeDeviceNotificationHandle(deviceNotificationHandle);
					Volatile.Write(ref _deviceNotificationHandle, handle);

					// If there is no bug, an instance should never be disposed when we reach this part of the method.
					// But because we can't throw exceptions from this method (it could be called from native code), try to handle this gracefully somehow.
					if (StateInitialized != Interlocked.CompareExchange(ref _state, StateInitialized, StateInitializing))
					{
						handle.Dispose();
					}
				}
			}

			internal abstract void OnDeviceArrival();
			internal abstract bool OnDeviceQueryRemove();
			internal abstract void OnDeviceQueryRemoveFailed();
			internal abstract void OnDeviceRemovePending();
			internal abstract void OnDeviceRemoveComplete();
		}

		private sealed class DeviceFileNotificationRegistration<T> : DeviceFileNotificationRegistration
		{
			/// <summary>A user-supplied object used to track this instance.</summary>
			public T UserToken { get; }

			/// <summary>The sink that will receive notifications for this instance.</summary>
			public IDeviceHandleNotificationSink<T> Sink { get; }

			public DeviceFileNotificationRegistration(DeviceNotificationEngine engine, SafeFileHandle fileHandle, T userToken, IDeviceHandleNotificationSink<T> sink)
				: base(engine, fileHandle)
			{
				UserToken = userToken;
				Sink = sink;
			}

			internal override void OnDeviceArrival() => Sink.OnDeviceArrival(DeviceFileHandle, UserToken, this);
			internal override bool OnDeviceQueryRemove() => Sink.OnDeviceQueryRemove(DeviceFileHandle, UserToken, this);
			internal override void OnDeviceQueryRemoveFailed() => Sink.OnDeviceQueryRemoveFailed(DeviceFileHandle, UserToken, this);
			internal override void OnDeviceRemovePending() => Sink.OnDeviceRemovePending(DeviceFileHandle, UserToken, this);
			internal override void OnDeviceRemoveComplete() => Sink.OnDeviceRemoveComplete(DeviceFileHandle, UserToken, this);
		}

		// Class used to manage device interface class notification registrations.
		// Multiple registrations with the same scope will reuse the same registration.
		// Each Sink gets its IDisposable instance, but notifications are registered only once per scope.
		private sealed class DeviceInterfaceClassSharedNotificationRegistration
		{
			// Null if the object was "disposed".
			private IDeviceNotificationSink[]? _sinks = Array.Empty<IDeviceNotificationSink>();

			public Guid? DeviceInterfaceClassGuid { get; }

			// Allocated upon first use, then deallocated upon last use. After last use, the object will be rendered unusable.
			private SafeDeviceNotificationHandle? _safeDeviceNotificationHandle;

			private readonly DeviceNotificationEngine _engine;

			internal DeviceInterfaceClassSharedNotificationRegistration(DeviceNotificationEngine engine)
			{
				_engine = engine;
			}

			internal DeviceInterfaceClassSharedNotificationRegistration(DeviceNotificationEngine engine, Guid deviceInterfaceClassGuid)
			{
				_engine = engine;
				DeviceInterfaceClassGuid = deviceInterfaceClassGuid;
			}

			public bool TryRegister(IDeviceNotificationSink sink, [NotNullWhen(true)] out IDisposable? registration)
			{
				lock (this)
				{
					var sinks = _sinks;

					if (sinks is null)
					{
						registration = null;
						return false;
					}

					if (Array.IndexOf(sinks, sink) < 0)
					{
						Array.Resize(ref sinks, sinks.Length + 1);
						sinks[^1] = sink;
						Volatile.Write(ref _sinks, sinks);

						if (sinks.Length == 1)
						{
							try
							{
								_safeDeviceNotificationHandle = RegisterDeviceNotification(_engine._targetHandle, _engine._isServiceHandle, DeviceInterfaceClassGuid);
							}
							catch
							{
								Dispose();
								throw;
							}
						}

						registration = new DeviceInterfaceClassNotificationRegistration(this, sink);
						return true;
					}
					else
					{
						throw new InvalidOperationException("This sink was already registered for the same notifications.");
					}
				}
			}

			private void Unregister(IDeviceNotificationSink sink)
			{
				lock (this)
				{
					var sinks = _sinks;

					ObjectDisposedException.ThrowIf(sinks is null, typeof(DeviceInterfaceClassSharedNotificationRegistration));

					if (Array.IndexOf(sinks, sink) is int index and >= 0)
					{
						if (sinks.Length == 1)
						{
							Dispose();
						}
						else
						{
							var newSinks = new IDeviceNotificationSink[sinks.Length - 1];
							Array.Copy(sinks, 0, newSinks, 0, index);
							Array.Copy(sinks, index, newSinks, index + 1, newSinks.Length - index);
							Volatile.Write(ref _sinks, sinks);
						}
					}
					else
					{
						throw new InvalidOperationException("Tried to unregister a sink that was already unregistered.");
					}
				}
			}

			private void Dispose()
			{
				Volatile.Write(ref _sinks, null);

				// NB: Code below is a bit safer than strictly required.
				if (DeviceInterfaceClassGuid is null)
				{
					Interlocked.CompareExchange(ref _engine._globalDeviceInterfaceClassRegistration, null, this);
				}
				else
				{
					SafeRemove(_engine._deviceInterfaceClassNotificationRegistrations, DeviceInterfaceClassGuid.GetValueOrDefault(), this);
				}
				_safeDeviceNotificationHandle?.Dispose();
			}

			internal void OnDeviceArrival(Guid deviceInterfaceClassGuid, string deviceName)
			{
				List<Exception>? exceptions = null;

				if (Volatile.Read(ref _sinks) is not null and var sinks)
				{
					foreach (var sink in sinks)
					{
						try
						{
							sink.OnDeviceArrival(deviceInterfaceClassGuid, deviceName);
						}
						catch (Exception ex)
						{
							(exceptions ?? new List<Exception>()).Add(ex);
						}
					}
				}
			}

			internal bool OnDeviceQueryRemove(Guid deviceInterfaceClassGuid, string deviceName)
			{
				List<Exception>? exceptions = null;

				bool result = true;

				if (Volatile.Read(ref _sinks) is not null and var sinks)
				{
					if (sinks is not null)
					{
						foreach (var sink in sinks)
						{
							try
							{
								if (!sink.OnDeviceQueryRemove(deviceInterfaceClassGuid, deviceName))
								{
									result = false;
								}
							}
							catch (Exception ex)
							{
								(exceptions ?? new List<Exception>()).Add(ex);
							}
						}
					}
				}
				return result;
			}

			internal void OnDeviceQueryRemoveFailed(Guid deviceInterfaceClassGuid, string deviceName)
			{
				List<Exception>? exceptions = null;

				if (Volatile.Read(ref _sinks) is not null and var sinks)
				{
					foreach (var sink in sinks)
					{
						try
						{
							sink.OnDeviceQueryRemoveFailed(deviceInterfaceClassGuid, deviceName);
						}
						catch (Exception ex)
						{
							(exceptions ?? new List<Exception>()).Add(ex);
						}
					}
				}
			}

			internal void OnDeviceRemovePending(Guid deviceInterfaceClassGuid, string deviceName)
			{
				List<Exception>? exceptions = null;

				if (Volatile.Read(ref _sinks) is not null and var sinks)
				{
					foreach (var sink in sinks)
					{
						try
						{
							sink.OnDeviceRemovePending(deviceInterfaceClassGuid, deviceName);
						}
						catch (Exception ex)
						{
							(exceptions ?? new List<Exception>()).Add(ex);
						}
					}
				}
			}

			internal void OnDeviceRemoveComplete(Guid deviceInterfaceClassGuid, string deviceName)
			{
				List<Exception>? exceptions = null;

				if (Volatile.Read(ref _sinks) is not null and var sinks)
				{
					foreach (var sink in sinks)
					{
						try
						{
							sink.OnDeviceRemoveComplete(deviceInterfaceClassGuid, deviceName);
						}
						catch (Exception ex)
						{
							(exceptions ?? new List<Exception>()).Add(ex);
						}
					}
				}
			}

			// Per-sink instance of a notification registration for device interface class(es).
			private class DeviceInterfaceClassNotificationRegistration : DeviceNotificationRegistrationBase
			{
				/// <summary>The sink that will receive notifications for this instance.</summary>
				public IDeviceNotificationSink Sink { get; }

				private DeviceInterfaceClassSharedNotificationRegistration? _sharedRegistration;

				public override bool IsDisposed => Volatile.Read(ref _sharedRegistration) == null;

				public DeviceInterfaceClassNotificationRegistration(DeviceInterfaceClassSharedNotificationRegistration sharedRegistration, IDeviceNotificationSink sink)
				{
					_sharedRegistration = sharedRegistration;
					Sink = sink;
				}

				// Notifications could still occur shortly after the Dispose method is called.
				protected override void Dispose(bool disposing) => Interlocked.Exchange(ref _sharedRegistration, null)?.Unregister(Sink);
			}
		}
	}
}
