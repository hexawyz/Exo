using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private static class SystemDevice
	{
		// Dock receivers are acting simultaneously as receivers and as a dock for a single device, unlike newer USB receivers that are dongle. (e.g. Razer Mamba Chroma Dock)
		// The code here will be very similar to but simpler than UsbReceiver, as these device do not support pairing with other devices than the one they came with.
		// (Well, they probably do support re-pairing with another identical device, but I don't even know if Synapse is able to do this, so we should clearly not care for now)
		// TODO: Implement the lighting for the dock. (As a technical "children" of the mouse device)
		public sealed class DockReceiver : RazerDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;
			private readonly IDriverRegistry _driverRegistry;
			private RazerDeviceDriver? _pairedDevice;
			private readonly AsyncLock _childDeviceLock;

			public DockReceiver
			(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				IDriverRegistry driverRegistry,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex,
				RazerDeviceFlags deviceFlags
			) : base(transport, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
			{
				_driverRegistry = driverRegistry;
				_childDeviceLock = new();
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.MouseDock;

			protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
			{
				using (await _childDeviceLock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					// Do a fake battery level reading to detect if the dock has been initialized.
					// These "dumb" dock devices will cache all responses from the mouse after it becomes online the first time, but will return an error before that.
					// The mouse will take lighting decisions for the dock, but the dock can buffer lighting changes for when the mouse comes back up.
					try
					{
						var r = await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false);
					}
					catch
					{
						return;
					}

					// If the above call succeeded, we proceed on to create the device.
					await HandleDeviceArrivalAsync(cancellationToken).ConfigureAwait(false);
				}
			}

			protected override void OnDeviceAvailabilityChange(byte notificationStreamIndex) => HandleChildDeviceStateChange();

			private void HandleChildDeviceStateChange()
				=> _ = HandleChildDeviceStateChangeAsync(default);

			private async ValueTask HandleChildDeviceStateChangeAsync(CancellationToken cancellationToken)
			{
				try
				{
					using (await _childDeviceLock.WaitAsync(cancellationToken).ConfigureAwait(false))
					{
						// NB: Maybe some newer dock devices support proper device offline notifications, so I left that path in the code. However, the Mamba Chroma will never appear offline once initialized.
						// TODO: Investigate more about a wait to test for device availability.
						try
						{
							var r = await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false);
						}
						catch
						{
							// If the call failed, we interpret that as the device being offline.
							await HandleDeviceRemovalAsync(cancellationToken).ConfigureAwait(false);
							return;
						}

						await HandleDeviceArrivalAsync(cancellationToken).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
				}
			}

			private async Task HandleDeviceArrivalAsync(CancellationToken cancellationToken)
			{
				// Don't recreate a driver if one is already present.
				if (Volatile.Read(ref _pairedDevice) is not null) return;

				var deviceInformation = GetDeviceInformation(_deviceIds[_mainDeviceIdIndex].ProductId);

				// TODO: Log unsupported device.
				if (Unsafe.IsNullRef(in deviceInformation)) return;

				// We use a similar logic as for other USB receivers here, however we could avoid doing the receiver info lookup because the device ID is always known in advance.
				if (deviceInformation.IsReceiver)
				{
					deviceInformation = GetDeviceInformation(deviceInformation.WiredDeviceProductId);

					if (Unsafe.IsNullRef(in deviceInformation)) return;
				}

				RazerDeviceDriver driver;

				try
				{
					var serialNumber = await _transport.GetSerialNumberAsync(default).ConfigureAwait(false);

					driver = await CreateChildDeviceAsync
					(
						_transport,
						DeviceIdSource.Unknown,
						0xFFFF,
						0,
						deviceInformation,
						deviceInformation.FriendlyName,
						ConfigurationKey.DeviceMainId,
						serialNumber,
						cancellationToken
					).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					return;
				}

				try
				{
					await _driverRegistry.AddDriverAsync(driver).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					await driver.DisposeAsync().ConfigureAwait(false);
					return;
				}

				if (Interlocked.Exchange(ref _pairedDevice, driver) is { } oldDriver)
				{
					// TODO: Log an error. We should never have to replace a live driver by another.

					await RemoveAndDisposeDriverAsync(oldDriver).ConfigureAwait(false);
				}
			}

			private async Task HandleDeviceRemovalAsync(CancellationToken cancellationToken)
			{
				if (Interlocked.Exchange(ref _pairedDevice, null) is { } oldDriver)
				{
					await RemoveAndDisposeDriverAsync(oldDriver).ConfigureAwait(false);
				}
			}

			private async Task RemoveAndDisposeDriverAsync(RazerDeviceDriver driver)
			{
				try
				{
					await _driverRegistry.RemoveDriverAsync(driver).ConfigureAwait(false);
					await driver.DisposeAsync().ConfigureAwait(false);
				}
				catch
				{
					// TODO: Log
				}
			}
		}

		// USB receivers are always root, so they are always system devices, unlike those which can be connected through a receiver.
		public sealed class UsbReceiver : RazerDeviceDriver
		{
			private struct PairedDeviceState
			{
				public RazerDeviceDriver? Driver;
				public ushort ProductId;

				public PairedDeviceState(ushort productId) : this() => ProductId = productId;
			}

			private readonly RazerDeviceNotificationWatcher _watcher;
			private readonly RazerDeviceNotificationWatcher? _secondWatcher;
			private readonly IDriverRegistry _driverRegistry;
			// As of now, there can be only two devices, but we can use an array here to be more future-proof. (Still need to understand how to address these other devices)
			private PairedDeviceState[]? _pairedDevices;

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;

			public UsbReceiver(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				DeviceStream? secondNotificationStream,
				DeviceNotificationOptions secondNotificationOptions,
				IDriverRegistry driverRegistry,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, RazerDeviceFlags.None)
			{
				_driverRegistry = driverRegistry;
				_watcher = new(notificationStream, this, deviceNotificationOptions);
				if (secondNotificationStream is not null)
				{
					_secondWatcher = new(secondNotificationStream, this, secondNotificationOptions);
				}
			}

			protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
			{
				await base.InitializeAsync(cancellationToken).ConfigureAwait(false);

				var childDevices = await _transport.GetDevicePairingInformationAsync(cancellationToken).ConfigureAwait(false);
				var pairedDevices = new PairedDeviceState[childDevices.Length];
				for (int i = 0; i < childDevices.Length; i++)
				{
					var device = childDevices[i];
					pairedDevices[i] = new(device.ProductId);
					if (device.IsConnected)
					{
						await HandleNewDeviceAsync(pairedDevices, i + 1, cancellationToken).ConfigureAwait(false);
					}
				}
				Volatile.Write(ref _pairedDevices, pairedDevices);
			}

			public override async ValueTask DisposeAsync()
			{
				_driverRegistry.Dispose();
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
				if (_secondWatcher is not null)
				{
					await _secondWatcher.DisposeAsync().ConfigureAwait(false);
				}
			}

			// TODO: Properly handle multi-device.
			protected override void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				// TODO: Log invalid device index.
				if (deviceIndex > pairedDevices.Length) return;

				HandleNewDevice(deviceIndex);
			}

			// TODO: Properly handle multi-device.
			protected override void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId)
				=> OnDeviceArrival(notificationStreamIndex, deviceIndex);

			// TODO: Properly handle multi-device.
			protected override void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				// TODO: Log invalid device index.
				if (deviceIndex > pairedDevices.Length) return;

				if (Interlocked.Exchange(ref pairedDevices[deviceIndex - 1].Driver, null) is { } oldDriver)
				{
					RemoveAndDisposeDriver(oldDriver);
				}
			}

			// TODO: Properly handle multi-device.
			protected override void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId)
				=> OnDeviceRemoval(notificationStreamIndex, deviceIndex);

			private async void RemoveAndDisposeDriver(RazerDeviceDriver driver)
			{
				try
				{
					await _driverRegistry.RemoveDriverAsync(driver).ConfigureAwait(false);
					DisposeDriver(driver);
				}
				catch
				{
					// TODO: Log
				}
			}

			// This method is only asynchronous in case of error
			private void HandleNewDevice(int deviceIndex)
				=> HandleNewDeviceAsync(deviceIndex, default).ConfigureAwait(false).GetAwaiter().GetResult();

			private ValueTask HandleNewDeviceAsync(int deviceIndex, CancellationToken cancellationToken)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return ValueTask.CompletedTask;

				// Need to see how to handle devices other than the main one.
				if (deviceIndex != 1) return ValueTask.CompletedTask;

				return HandleNewDeviceAsync(pairedDevices, deviceIndex, cancellationToken);
			}

			private async ValueTask HandleNewDeviceAsync(PairedDeviceState[] pairedDevices, int deviceIndex, CancellationToken cancellationToken)
			{
				int stateIndex = deviceIndex - 1;

				// Don't recreate a driver if one is already present.
				if (Volatile.Read(ref pairedDevices[stateIndex].Driver) is not null) return;

				var basicDeviceInformation = await _transport.GetDeviceInformationAsync(cancellationToken).ConfigureAwait(false);

				// Update the state in case the paired device has changed.
				if (pairedDevices[stateIndex].ProductId != basicDeviceInformation.ProductId)
				{
					pairedDevices[stateIndex].ProductId = basicDeviceInformation.ProductId;
				}

				// If the device is already connected, skip everything else.
				if (!basicDeviceInformation.IsConnected) return;

				var deviceInformation = GetDeviceInformation(basicDeviceInformation.ProductId);

				// TODO: Log unsupported device.
				if (Unsafe.IsNullRef(in deviceInformation)) return;

				// Child devices would generally share a PID with their USB receiver, we need to get the information for the device and not the receiver.
				if (deviceInformation.IsReceiver)
				{
					deviceInformation = GetDeviceInformation(deviceInformation.WiredDeviceProductId);

					if (Unsafe.IsNullRef(in deviceInformation)) return;
				}

				RazerDeviceDriver driver;

				try
				{
					var serialNumber = await _transport.GetSerialNumberAsync(default).ConfigureAwait(false);

					driver = await CreateChildDeviceAsync
					(
						_transport,
						DeviceIdSource.Unknown,
						0xFFFF,
						(byte)deviceIndex,
						deviceInformation,
						deviceInformation.FriendlyName,
						ConfigurationKey.DeviceMainId,
						serialNumber,
						cancellationToken
					).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					return;
				}

				try
				{
					await _driverRegistry.AddDriverAsync(driver).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					DisposeDriver(driver);
					return;
				}

				if (Interlocked.Exchange(ref pairedDevices[stateIndex].Driver, driver) is { } oldDriver)
				{
					// TODO: Log an error. We should never have to replace a live driver by another.

					await _driverRegistry.RemoveDriverAsync(oldDriver).ConfigureAwait(false);
					DisposeDriver(oldDriver);
				}
			}

			protected override void OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				if (Volatile.Read(ref pairedDevices[deviceIndex - 1].Driver) is { } driver)
				{
					driver.OnDeviceDpiChange(dpiX, dpiY);
				}
			}

			protected override void OnDeviceExternalPowerChange(byte deviceIndex, bool isCharging)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				if (Volatile.Read(ref pairedDevices[deviceIndex - 1].Driver) is { } driver)
				{
					driver.OnDeviceExternalPowerChange(isCharging);
				}
			}

			protected override void OnDeviceBatteryLevelChange(byte deviceIndex, byte batteryLevel)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				if (Volatile.Read(ref pairedDevices[deviceIndex - 1].Driver) is { } driver)
				{
					driver.OnDeviceBatteryLevelChange(batteryLevel);
				}
			}

			// This is some kind of fire and forget driver disposal, but we always catch the exceptions.
			private async void DisposeDriver(RazerDeviceDriver driver)
			{
				try
				{
					await driver.DisposeAsync().ConfigureAwait(false);
				}
				catch
				{
					// TODO: Log exception.
				}
			}
		}

		public class Generic : RazerDeviceDriver.Generic
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public Generic
			(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				DeviceCategory deviceCategory,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, deviceCategory, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}

		public class Mouse : RazerDeviceDriver.Mouse
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			public Mouse
			(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				Guid lightingZoneId,
				ushort maximumDpi,
				ushort maximumPollingFrequency,
				ImmutableArray<byte> supportedPollingFrequencyDividerPowers,
				DotsPerInch[] initialDpiPresets,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, lightingZoneId, maximumDpi, maximumPollingFrequency, supportedPollingFrequencyDividerPowers, initialDpiPresets, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}

		public class Keyboard : RazerDeviceDriver.Keyboard
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			public Keyboard(
				IRazerProtocolTransport transport,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}
	}
}
