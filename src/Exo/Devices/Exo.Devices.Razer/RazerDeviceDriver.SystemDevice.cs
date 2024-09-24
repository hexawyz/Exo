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
