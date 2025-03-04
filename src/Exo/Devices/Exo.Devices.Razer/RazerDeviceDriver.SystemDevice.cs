using System.Collections.Immutable;
using DeviceTools;

namespace Exo.Devices.Razer;

public abstract partial class RazerDeviceDriver
{
	private static partial class SystemDevice
	{
		public class Generic : RazerDeviceDriver.Generic
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public Generic
			(
				IRazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				DeviceCategory deviceCategory,
				in DeviceInformation deviceInformation,
				ImmutableArray<RazerLedId> ledIds,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceConnectionType connectionType,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, deviceCategory, deviceInformation, ledIds, friendlyName, configurationKey, deviceFlags, connectionType, deviceIds, mainDeviceIdIndex)
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
				RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				in DeviceInformation deviceInformation,
				ImmutableArray<RazerLedId> ledIds,
				ushort maximumDpi,
				ushort maximumPollingFrequency,
				ImmutableArray<byte> supportedPollingFrequencyDividerPowers,
				DotsPerInch[] initialDpiPresets,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceConnectionType connectionType,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base
			(
				transport,
				periodicEventGenerator,
				deviceInformation,
				ledIds,
				maximumDpi,
				maximumPollingFrequency,
				supportedPollingFrequencyDividerPowers,
				initialDpiPresets,
				friendlyName,
				configurationKey,
				deviceFlags,
				connectionType,
				deviceIds,
				mainDeviceIdIndex
			)
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
				RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
				DeviceStream notificationStream,
				DeviceNotificationOptions deviceNotificationOptions,
				in DeviceInformation deviceInformation,
				ImmutableArray<RazerLedId> ledIds,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceConnectionType connectionType,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, deviceInformation, ledIds, friendlyName, configurationKey, deviceFlags, connectionType, deviceIds, mainDeviceIdIndex)
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
