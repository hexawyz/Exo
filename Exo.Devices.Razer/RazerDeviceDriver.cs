using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

// Like the Logitech driver, this will likely benefit from a refactoring of the device discovery, allowing to create drivers with different features on-demand.
// For now, it will only exactly support the features for Razer DeathAdder V2 & Dock, but it will need more flexibility to support other devices using the same protocol.
// NB: This driver relies on system drivers provided by Razer to access device features. The protocol part is still implemented here, but we need the driver to get access to the device.
[ProductId(VendorIdSource.Usb, 0x1532, 0x007C)] // Mouse
[ProductId(VendorIdSource.Usb, 0x1532, 0x007D)] // Mouse via Dongle
[ProductId(VendorIdSource.Usb, 0x1532, 0x007E)] // Dock
public abstract class RazerDeviceDriver : Driver, IRazerDeviceNotificationSink
{
	// It does not seem we can retrieve enough metadata from the devices themselves, so we need to have some manually entered data here.
	private readonly struct DeviceInformation
	{
		public DeviceInformation(RazerDeviceCategory deviceCategory, ushort actualDeviceProductId, Guid? lightingZoneGuid, string friendlyName)
		{
			DeviceCategory = deviceCategory;
			ActualDeviceProductId = actualDeviceProductId;
			LightingZoneGuid = lightingZoneGuid;
			FriendlyName = friendlyName;
		}

		public RazerDeviceCategory DeviceCategory { get; }

		// Razer devices seem to share the same PID between the device and the dongle it was delivered with.
		// This property indicates the product ID containing the information of the device associated with a dongle PID.
		public ushort ActualDeviceProductId { get; }

		public Guid? LightingZoneGuid { get; }
		public string FriendlyName { get; }
	}

	// This is a purely internal enum to provide metadata about the devices.
	private enum RazerDeviceCategory : byte
	{
		Unknown = 0,
		Keyboard = 1,
		Mouse = 2,
		UsbReceiver = 3,
		Dock = 4,
	}

	private static readonly Guid RazerControlDeviceInterfaceClassGuid = new Guid(0xe3be005d, 0xd130, 0x4910, 0x88, 0xff, 0x09, 0xae, 0x02, 0xf6, 0x80, 0xe9);

	private static readonly Guid DockLightingZoneGuid = new(0x5E410069, 0x0F34, 0x4DD8, 0x80, 0xDB, 0x5B, 0x11, 0xFB, 0xD4, 0x13, 0xD6);
	private static readonly Guid DeathAdderV2ProLightingZoneGuid = new(0x4D2EE313, 0xEA46, 0x4857, 0x89, 0x8C, 0x5B, 0xF9, 0x44, 0x09, 0x0A, 0x9A);

	private static readonly Dictionary<ushort, DeviceInformation> DeviceInformations = new()
	{
		{ 0x007C, new(RazerDeviceCategory.Mouse, 0x007C, DeathAdderV2ProLightingZoneGuid, "Razer DeathAdder V2 Pro") },
		{ 0x007D, new(RazerDeviceCategory.UsbReceiver, 0x007C, null, "Razer DeathAdder V2 Pro HyperSpeed Dongle") },
		{ 0x007E, new(RazerDeviceCategory.Dock, 0x007E, DockLightingZoneGuid, "Razer Mouse Dock") },
	};

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	};

	public static async Task<RazerDeviceDriver> CreateAsync(string deviceName, ushort productId, Optional<IDriverRegistry> driverRegistry, CancellationToken cancellationToken)
	{
		// Start by retrieving the local device metadata. It will throw if missing, which is what we want.
		// We need this for two reasons:
		// 1 - This is less than ideal, but it seems we can't retrieve enough information from the device themselves (type of device, etc.)
		// 2 - We need predefined lighting zone GUIDs. (Maybe we could generate these from VID/PID pairs to avoid this manual mapping ?)
		var deviceInfo = DeviceInformations[productId];

		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the matching HID device interfaces at once.
		var hidDeviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		// Make a device query to fetch the razer control device interface.
		var razerControlDeviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == RazerControlDeviceInterfaceClassGuid &
				Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		if (razerControlDeviceInterfaces.Length != 1)
		{
			throw new InvalidOperationException("Expected a single device interface for Razer device control.");
		}

		string razerControlDeviceName = razerControlDeviceInterfaces[0].Id;

		// Find the top-level device by requesting devices with children.
		// The device tree should be very simple in this case, so we expect this to directly return the top level device. It would not work on more complex scenarios.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			Array.Empty<Property>(),
			Properties.System.Devices.ContainerId == containerId & Properties.System.Devices.Children.Exists(),
			cancellationToken
		).ConfigureAwait(false);

		string[] deviceNames = new string[hidDeviceInterfaces.Length + 2];
		string topLevelDeviceName = devices[0].Id;
		string? notificationDeviceName = null;

		// Set the razer control device as the first device name for now.
		deviceNames[0] = razerControlDeviceName;

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDeviceName;

		for (int i = 0; i < hidDeviceInterfaces.Length; i++)
		{
			var deviceInterface = hidDeviceInterfaces[i];
			deviceNames[i + 1] = deviceInterface.Id;

			if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
				deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
				usagePage == (ushort)HidUsagePage.GenericDesktop && usageId == 0)
			{
				using (var deviceHandle = HidDevice.FromPath(deviceInterface.Id))
				{
					// TODO: Might finally be time to work on that HID descriptor part 😫
					// Also, I don't know why Windows insist on declaring the values we are looking for as button. The HID descriptor clearly indicates values between 0 and 255…
					var nodes = deviceHandle.GetButtonCapabilities(NativeMethods.HidParsingReportType.Input);

					// Thanks to the button caps we can check the Report ID.
					// We are looking for the HID device interface tied to the collection with Report ID 5.
					// (Remember this relatively annoying Windows-specific stuff of splitting interfaces by top-level collection)
					if (nodes[0].ReportID == 5)
					{
						if (notificationDeviceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device notifications.");

						notificationDeviceName = deviceInterface.Id;
					}
				}
			}
		}

		if (notificationDeviceName is null)
		{
			throw new InvalidOperationException("The device interface for device notifications was not found.");
		}

		var transport = new RazerProtocolTransport(Device.OpenHandle(razerControlDeviceName, DeviceAccess.None));

		string? serialNumber = null;

		if (deviceInfo.DeviceCategory != RazerDeviceCategory.UsbReceiver)
		{
			serialNumber = transport.GetSerialNumber();
		}

		var device = CreateDevice
		(
			transport,
			new(notificationDeviceName),
			driverRegistry,
			productId,
			deviceInfo,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName,
			topLevelDeviceName,
			serialNumber
		);

		// Get rid of the nested driver registry if we don't need it.
		if (device is not SystemDevice.UsbReceiver)
		{
			driverRegistry.Dispose();
		}

		return device;
	}

	private static RazerDeviceDriver CreateDevice
	(
		RazerProtocolTransport transport,
		HidFullDuplexStream notificationStream,
		Optional<IDriverRegistry> driverRegistry,
		ushort productId,
		DeviceInformation deviceInfo,
		ImmutableArray<string> deviceNames,
		string friendlyName,
		string topLevelDeviceName,
		string? serialNumber
	)
	{
		var configurationKey = new DeviceConfigurationKey("RazerDevice", topLevelDeviceName, $"Razer_Device_{productId:X4}", serialNumber);
		return deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new SystemDevice.Keyboard
			(
				transport,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey
			),
			RazerDeviceCategory.Mouse => new SystemDevice.Mouse
			(
				transport,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey
			),
			RazerDeviceCategory.Dock => new SystemDevice.Generic
			(
				transport,
				notificationStream,
				DeviceCategory.MouseDock,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey
			),
			RazerDeviceCategory.UsbReceiver => new SystemDevice.UsbReceiver
			(
				transport,
				notificationStream,
				driverRegistry.GetOrCreateValue(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
	}

	private static RazerDeviceDriver CreateChildDevice
	(
		RazerProtocolTransport transport,
		ushort productId,
		byte deviceIndex,
		DeviceInformation deviceInfo,
		string friendlyName,
		string topLevelDeviceName,
		string serialNumber
	)
	{
		var configurationKey = new DeviceConfigurationKey("RazerDevice", $"{topLevelDeviceName}#IX_{deviceIndex:X2}&PID_{productId:X4}", $"Razer_Device_{productId:X4}", serialNumber);

		return deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new Keyboard
			(
				transport,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey
			),
			RazerDeviceCategory.Mouse => new Mouse
			(
				transport,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey
			),
			RazerDeviceCategory.Dock => new Generic
			(
				transport,
				DeviceCategory.MouseDock,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
	}

	private readonly RazerProtocolTransport _transport;

	private RazerDeviceDriver(
		RazerProtocolTransport transport,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
	}

	// The transport must only be disposed for root devices.
	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	public virtual void OnDeviceArrival(byte deviceIndex)
	{
	}

	public virtual void OnDeviceRemoval(byte deviceIndex)
	{
	}

	public virtual void OnDeviceDpiChange(byte deviceIndex, ushort dpi1, ushort dpi2)
	{
	}

	private class Generic : BaseDevice
	{
		public override DeviceCategory DeviceCategory { get; }

		public Generic(
			RazerProtocolTransport transport,
			DeviceCategory deviceCategory,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey
		) : base(transport, lightingZoneId, friendlyName, configurationKey)
		{
			DeviceCategory = deviceCategory;
		}
	}

	private class Mouse : BaseDevice
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

		public Mouse(
			RazerProtocolTransport transport,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey
		) : base(transport, lightingZoneId, friendlyName, configurationKey)
		{
		}
	}

	private class Keyboard : BaseDevice
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

		public Keyboard(
			RazerProtocolTransport transport,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey
		) : base(transport, lightingZoneId, friendlyName, configurationKey)
		{
		}
	}

	// Classes implementing ISystemDeviceDriver and relying on their own Notification Watcher.
	private static class SystemDevice
	{
		// USB receivers are always root, so they are always system devices, unlike those which can be connected through a receiver.
		public sealed class UsbReceiver : RazerDeviceDriver, ISystemDeviceDriver
		{
			private struct PairedDeviceState
			{
				public RazerDeviceDriver? Driver;
				public readonly ushort ProductId;

				public PairedDeviceState(ushort productId) : this() => ProductId = productId;
			}

			private readonly RazerDeviceNotificationWatcher _watcher;
			private readonly IDriverRegistry _driverRegistry;
			// As of now, there can be only two devices, but we can use an array here to be more future-proof. (Still need to understand how to address these other devices)
			private readonly PairedDeviceState[] _pairedDevices;

			public ImmutableArray<string> DeviceNames { get; }

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;
			public override IDeviceFeatureCollection<IDeviceFeature> Features => FeatureCollection.Empty<IDeviceFeature>();

			public UsbReceiver(
				RazerProtocolTransport transport,
				HidFullDuplexStream notificationStream,
				IDriverRegistry driverRegistry,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey
			) : base(transport, friendlyName, configurationKey)
			{
				_driverRegistry = driverRegistry;
				DeviceNames = deviceNames;
				var childDevices = transport.GetDevicePairingInformation();
				_pairedDevices = new PairedDeviceState[childDevices.Length];
				for (int i = 0; i < childDevices.Length; i++)
				{
					var device = childDevices[i];
					_pairedDevices[i] = new(device.ProductId);
					if (device.IsConnected)
					{
						HandleNewDevice(i + 1);
					}
				}
				_watcher = new(notificationStream, this);
			}

			public override async ValueTask DisposeAsync()
			{
				_transport.Dispose();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}

			public override void OnDeviceArrival(byte deviceIndex)
			{
				// TODO: Log invalid device index.
				if (deviceIndex > _pairedDevices.Length) return;

				HandleNewDevice(deviceIndex);
			}

			public override void OnDeviceRemoval(byte deviceIndex)
			{
				// TODO: Log invalid device index.
				if (deviceIndex > _pairedDevices.Length) return;

				if (Interlocked.Exchange(ref _pairedDevices[deviceIndex - 1].Driver, null) is { } oldDriver)
				{
					_driverRegistry.RemoveDriver(oldDriver);
					DisposeDriver(oldDriver);
				}
			}

			// This method is only asynchronous in case of error
			private void HandleNewDevice(int deviceIndex)
			{
				// Need to see how to handle devices other than the main one.
				if (deviceIndex != 1) return;

				ref var state = ref _pairedDevices[deviceIndex - 1];

				// Don't recreate a driver if one is already present.
				if (Volatile.Read(ref state.Driver) is not null) return;

				// TODO: Refresh device info ?
				// I'm assuming we should receive notifications of some form when devices are paired or unpaired.
				if (state.ProductId == 0xFFFF) return;

				// TODO: Log unsupported device.
				if (!DeviceInformations.TryGetValue(state.ProductId, out var deviceInformation)) return;

				// Child devices would generally share a PID with their USB receiver, we need to get the information for the device and not the receiver.
				if (deviceInformation.ActualDeviceProductId != state.ProductId)
				{
					if (!DeviceInformations.TryGetValue(deviceInformation.ActualDeviceProductId, out deviceInformation)) return;
				}

				RazerDeviceDriver driver;

				try
				{
					var serialNumber = _transport.GetSerialNumber();

					driver = CreateChildDevice(_transport, state.ProductId, (byte)deviceIndex, deviceInformation, deviceInformation.FriendlyName, ConfigurationKey.DeviceMainId, serialNumber);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					return;
				}

				try
				{
					_driverRegistry.AddDriver(driver);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					DisposeDriver(driver);
					return;
				}

				if (Interlocked.Exchange(ref state.Driver, driver) is { } oldDriver)
				{
					// TODO: Log an error. We should never have to replace a live driver by another.

					_driverRegistry.RemoveDriver(oldDriver);
					DisposeDriver(oldDriver);
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

		public class Generic : RazerDeviceDriver.Generic, ISystemDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public ImmutableArray<string> DeviceNames { get; }

			public Generic(
				RazerProtocolTransport transport,
				HidFullDuplexStream notificationStream,
				DeviceCategory deviceCategory,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey
			) : base(transport, deviceCategory, lightingZoneId, friendlyName, configurationKey)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				_transport.Dispose();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}

		public class Mouse : RazerDeviceDriver.Mouse, ISystemDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public ImmutableArray<string> DeviceNames { get; }
			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			public Mouse(
				RazerProtocolTransport transport,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey
			) : base(transport, lightingZoneId, friendlyName, configurationKey)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				_transport.Dispose();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}

		public class Keyboard : RazerDeviceDriver.Keyboard, ISystemDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public ImmutableArray<string> DeviceNames { get; }
			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			public Keyboard(
				RazerProtocolTransport transport,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey
			) : base(transport, lightingZoneId, friendlyName, configurationKey)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				_transport.Dispose();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	private abstract class BaseDevice :
		RazerDeviceDriver,
		IDeviceDriver<ILightingDeviceFeature>,
		IUnifiedLightingFeature,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>
	{
		private ILightingEffect _appliedEffect;
		private ILightingEffect _currentEffect;
		private byte _currentBrightness;
		private readonly Guid _lightingZoneId;
		private readonly object _lock;
		private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
		private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
		// How do we use this ?
		private readonly byte _deviceIndex;

		protected BaseDevice(
			RazerProtocolTransport transport,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey
		) : base(transport, friendlyName, configurationKey)
		{
			_appliedEffect = DisabledEffect.SharedInstance;
			_currentEffect = DisabledEffect.SharedInstance;
			_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, BaseDevice, IUnifiedLightingFeature>(this);
			_allFeatures = FeatureCollection.Create<IDeviceFeature, BaseDevice, IUnifiedLightingFeature>(this);
			_lock = new();
			_lightingZoneId = lightingZoneId;
			_currentBrightness = 0x54; // 33%
			// Unless it is possible to retrieve the current settings from the device, we should reset the effect.
			ApplyEffect(DisabledEffect.SharedInstance, _currentBrightness, true);
		}

		IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
		public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

		// TODO
		bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

		ValueTask IUnifiedLightingFeature.ApplyChangesAsync()
		{
			lock (_lock)
			{
				if (!ReferenceEquals(_appliedEffect, _currentEffect))
				{
					ApplyEffect(_currentEffect, _currentBrightness, _appliedEffect is DisabledEffect);
					_appliedEffect = _currentEffect;
				}
			}
			return ValueTask.CompletedTask;
		}

		private void ApplyEffect(ILightingEffect effect, byte brightness, bool forceBrightnessUpdate)
		{
			if (ReferenceEquals(effect, DisabledEffect.SharedInstance))
			{
				_transport.SetStaticColor(default);
				_transport.SetBrightness(0);
				return;
			}

			// It seems brightness must be restored from zero first before setting a color effect.
			// Otherwise, the device might restore to its default effect. (e.g. Color Cycle)
			if (forceBrightnessUpdate)
			{
				_transport.SetBrightness(brightness);
			}

			switch (effect)
			{
			case StaticColorEffect staticColorEffect:
				_transport.SetStaticColor(staticColorEffect.Color);
				break;
			}
		}

		private void SetCurrentEffect(ILightingEffect effect)
		{
			lock (_lock)
			{
				_currentEffect = effect;
			}
		}

		// TODO: Determine how this should be exposed.
		public void SetDefaultBrightness(byte brightness)
		{
			_currentBrightness = brightness;
		}

		// TODO: Devices can support multiple lighting zones OR a single zone. We must support both scenarios.
		Guid ILightingZone.ZoneId => _lightingZoneId;

		ILightingEffect ILightingZone.GetCurrentEffect() => _currentEffect;

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => SetCurrentEffect(DisabledEffect.SharedInstance);
		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => SetCurrentEffect(effect);

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => _currentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => _currentEffect.TryGetEffect(out effect);
	}
}
