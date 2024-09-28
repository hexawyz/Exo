using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private static partial class SystemDevice
	{
		// Dock receivers are acting simultaneously as receivers and as a dock for a single device, unlike newer USB receivers that are dongle. (e.g. Razer Mamba Chroma Dock)
		// The code here will be very similar to but simpler than UsbReceiver, as these device do not support pairing with other devices than the one they came with.
		// (Well, they probably do support re-pairing with another identical device, but I don't even know if Synapse is able to do this, so we should clearly not care for now)
		// TODO: Implement the lighting for the dock. (As a technical "children" of the mouse device)
		public sealed class DockReceiver : BaseDevice
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
				in DeviceInformation deviceInformation,
				ImmutableArray<RazerLedId> ledIds,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex,
				RazerDeviceFlags deviceFlags
			) : base(transport, deviceInformation, ledIds, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
			{
				_driverRegistry = driverRegistry;
				_childDeviceLock = new();
				_watcher = new(notificationStream, this, deviceNotificationOptions);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.MouseDock;

			protected override (LightingZone? UnifiedLightingZone, ImmutableArray<LightingZone> LightingZones) CreateLightingZones(in DeviceInformation deviceInformation, ImmutableArray<RazerLedId> ledIds)
				=> (new DockLightingZoneV1(this, deviceInformation.LightingZoneGuid.GetValueOrDefault(), RazerLedId.Dongle), []);

			protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
			{
				await base.InitializeAsync(cancellationToken);

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
	}
}
