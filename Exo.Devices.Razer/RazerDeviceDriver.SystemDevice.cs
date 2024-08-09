using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	// Classes implementing ISystemDeviceDriver and relying on their own Notification Watcher.
	private static class SystemDevice
	{
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
			private readonly IDriverRegistry _driverRegistry;
			// As of now, there can be only two devices, but we can use an array here to be more future-proof. (Still need to understand how to address these other devices)
			private PairedDeviceState[]? _pairedDevices;

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;

			public UsbReceiver(
				IRazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				IDriverRegistry driverRegistry,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, RazerDeviceFlags.None)
			{
				_driverRegistry = driverRegistry;
				_watcher = new(notificationStream, this, deviceNotificationOptions);
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
			}

			protected override void OnDeviceArrival(byte deviceIndex)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				// TODO: Log invalid device index.
				if (deviceIndex > pairedDevices.Length) return;

				HandleNewDevice(deviceIndex);
			}

			protected override void OnDeviceRemoval(byte deviceIndex)
			{
				if (Volatile.Read(ref _pairedDevices) is not { } pairedDevices) return;

				// TODO: Log invalid device index.
				if (deviceIndex > pairedDevices.Length) return;

				if (Interlocked.Exchange(ref pairedDevices[deviceIndex - 1].Driver, null) is { } oldDriver)
				{
					RemoveAndDisposeDriver(oldDriver);
				}
			}

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

				// If the device is already disconnected, skip everything else.
				if (!basicDeviceInformation.IsConnected) return;

				var deviceInformation = GetDeviceInformation(basicDeviceInformation.ProductId);

				// TODO: Log unsupported device.
				if (Unsafe.IsNullRef(in deviceInformation)) return;

				// Child devices would generally share a PID with their USB receiver, we need to get the information for the device and not the receiver.
				if (deviceInformation.IsDongle)
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
						_periodicEventGenerator,
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
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				DeviceCategory deviceCategory,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, deviceCategory, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
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
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
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
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
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
