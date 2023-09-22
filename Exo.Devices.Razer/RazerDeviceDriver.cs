using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Razer.LightingEffects;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Features.MouseFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

// Like the Logitech driver, this will likely benefit from a refactoring of the device discovery, allowing to create drivers with different features on-demand.
// For now, it will only exactly support the features for Razer DeathAdder V2 & Dock, but it will need more flexibility to support other devices using the same protocol.
// NB: This driver relies on system drivers provided by Razer to access device features. The protocol part is still implemented here, but we need the driver to get access to the device.
[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007C)] // Mouse
[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007D)] // Mouse via Dongle
[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007E)] // Dock
public abstract class RazerDeviceDriver :
	Driver,
	IRazerDeviceNotificationSink,
	IRazerPeriodicEventHandler,
	ISerialNumberDeviceFeature,
	IDeviceIdFeature
{
	private const ushort RazerVendorId = 0x1532;

	// It does not seem we can retrieve enough metadata from the devices themselves, so we need to have some manually entered data here.
	private readonly struct DeviceInformation
	{
		public DeviceInformation(RazerDeviceCategory deviceCategory, RazerDeviceFlags flags, ushort actualDeviceProductId, Guid? lightingZoneGuid, string friendlyName)
		{
			DeviceCategory = deviceCategory;
			Flags = flags;
			ActualDeviceProductId = actualDeviceProductId;
			LightingZoneGuid = lightingZoneGuid;
			FriendlyName = friendlyName;
		}

		public RazerDeviceCategory DeviceCategory { get; }

		public RazerDeviceFlags Flags { get; }

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

	[Flags]
	private enum RazerDeviceFlags : byte
	{
		None = 0x00,
		HasBattery = 0x01,
		HasReactiveLighting = 0x80,
	}

	private static readonly Guid RazerControlDeviceInterfaceClassGuid = new Guid(0xe3be005d, 0xd130, 0x4910, 0x88, 0xff, 0x09, 0xae, 0x02, 0xf6, 0x80, 0xe9);

	private static readonly Guid DockLightingZoneGuid = new(0x5E410069, 0x0F34, 0x4DD8, 0x80, 0xDB, 0x5B, 0x11, 0xFB, 0xD4, 0x13, 0xD6);
	private static readonly Guid DeathAdderV2ProLightingZoneGuid = new(0x4D2EE313, 0xEA46, 0x4857, 0x89, 0x8C, 0x5B, 0xF9, 0x44, 0x09, 0x0A, 0x9A);

	private static readonly Dictionary<ushort, DeviceInformation> DeviceInformations = new()
	{
		{ 0x007C, new(RazerDeviceCategory.Mouse, RazerDeviceFlags.HasBattery | RazerDeviceFlags.HasReactiveLighting, 0x007C, DeathAdderV2ProLightingZoneGuid, "Razer DeathAdder V2 Pro") },
		{ 0x007D, new(RazerDeviceCategory.UsbReceiver, RazerDeviceFlags.None, 0x007C, null, "Razer DeathAdder V2 Pro HyperSpeed Dongle") },
		{ 0x007E, new(RazerDeviceCategory.Dock, RazerDeviceFlags.None, 0x007E, DockLightingZoneGuid, "Razer Mouse Dock") },
	};

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	};

	private static readonly Property[] RequestedDeviceProperties = new Property[]
	{
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
	};

	public static async Task<RazerDeviceDriver> CreateAsync(string deviceName, ushort productId, ushort version, Optional<IDriverRegistry> driverRegistry, CancellationToken cancellationToken)
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
			RequestedDeviceProperties,
			Properties.System.Devices.ContainerId == containerId &
			Properties.System.Devices.Children.Exists() &
			Properties.System.Devices.ClassGuid != DeviceClassGuids.Hid &
			Properties.System.Devices.BusTypeGuid != DeviceBusTypesGuids.Hid,
			cancellationToken
		).ConfigureAwait(false);

		string[] deviceNames = new string[hidDeviceInterfaces.Length + 2];
		var topLevelDevice = devices[0];
		string? notificationDeviceName = null;
		DeviceIdSource deviceIdSource = DeviceIdSource.Unknown;

		// Set the razer control device as the first device name for now.
		deviceNames[0] = razerControlDeviceName;

		{
			Guid guid;
			if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.BusTypeGuid.Key, out guid))
			{
				if (guid == DeviceBusTypesGuids.Usb)
				{
					deviceIdSource = DeviceIdSource.Usb;
					goto DeviceIdSourceResolved;
				}
			}

			if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.ClassGuid.Key, out guid))
			{
				if (guid == DeviceClassGuids.Usb)
				{
					deviceIdSource = DeviceIdSource.Usb;
					goto DeviceIdSourceResolved;
				}
				if (guid == DeviceClassGuids.Bluetooth)
				{
					if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.EnumeratorName.Key, out string? enumeratorName))
					{
						if (enumeratorName == "BTHLE")
						{
							deviceIdSource = DeviceIdSource.BluetoothLowEnergy;
							goto DeviceIdSourceResolved;
						}
						deviceIdSource = DeviceIdSource.Bluetooth;
						goto DeviceIdSourceResolved;
					}
				}
			}
		DeviceIdSourceResolved:;
		}

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDevice.Id;

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
			new(60_000),
			new(notificationDeviceName),
			driverRegistry,
			deviceIdSource,
			productId,
			version,
			deviceInfo,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName,
			topLevelDevice.Id,
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
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		HidFullDuplexStream notificationStream,
		Optional<IDriverRegistry> driverRegistry,
		DeviceIdSource deviceIdSource,
		ushort productId,
		ushort versionNumber,
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
				periodicEventGenerator,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			RazerDeviceCategory.Mouse => new SystemDevice.Mouse
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			RazerDeviceCategory.Dock => new SystemDevice.Generic
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				DeviceCategory.MouseDock,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			RazerDeviceCategory.UsbReceiver => new SystemDevice.UsbReceiver
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				driverRegistry.GetOrCreateValue(),
				deviceNames,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceIdSource,
				productId,
				versionNumber
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
	}

	private static RazerDeviceDriver CreateChildDevice
	(
		RazerProtocolTransport transport,
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		DeviceIdSource deviceIdSource,
		ushort productId,
		ushort versionNumber,
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
				periodicEventGenerator,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			RazerDeviceCategory.Mouse => new Mouse
			(
				transport,
				periodicEventGenerator,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			RazerDeviceCategory.Dock => new Generic
			(
				transport,
				periodicEventGenerator,
				DeviceCategory.MouseDock,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIdSource,
				productId,
				versionNumber
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
	}

	// The transport is used to communicate with the device.
	private readonly RazerProtocolTransport _transport;

	// The periodic event generator is used to manage periodic events on the transport.
	// It *could* be merged with the transport for practical reasons but the two features are not related enough.
	private readonly RazerProtocolPeriodicEventGenerator _periodicEventGenerator;

	private readonly DeviceIdSource _deviceIdSource;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;

	private RazerDeviceDriver
	(
		RazerProtocolTransport transport,
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		DeviceIdSource deviceIdSource,
		ushort productId,
		ushort versionNumber
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_periodicEventGenerator = periodicEventGenerator;
		_deviceIdSource = deviceIdSource;
		_productId = productId;
		_versionNumber = versionNumber;
	}

	// The transport must only be disposed for root devices.
	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	// Method used to dispose resources in root instances.
	// Child devices will share the parent resources, so they should not call this method.
	private void DisposeRootResources()
	{
		// Disposing the periodic event generator first should reduce the risk of errors related to accessing a disposed transport.
		_periodicEventGenerator.Dispose();
		_transport.Dispose();
	}

	void IRazerPeriodicEventHandler.HandlePeriodicEvent() => HandlePeriodicEvent();

	protected virtual void HandlePeriodicEvent() { }

	void IRazerDeviceNotificationSink.OnDeviceArrival(byte deviceIndex) => OnDeviceArrival(deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceRemoval(byte deviceIndex) => OnDeviceRemoval(deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY) => OnDeviceDpiChange(deviceIndex, dpiX, dpiY);
	void IRazerDeviceNotificationSink.OnDeviceExternalPowerChange(byte deviceIndex, bool isConnectedToExternalPower) => OnDeviceExternalPowerChange(deviceIndex, isConnectedToExternalPower);

	protected virtual void OnDeviceArrival(byte deviceIndex) { }
	protected virtual void OnDeviceRemoval(byte deviceIndex) { }
	protected virtual void OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY) { }
	protected virtual void OnDeviceExternalPowerChange(byte deviceIndex, bool isConnectedToExternalPower) { }

	protected virtual void OnDeviceDpiChange(ushort dpiX, ushort dpiY) { }
	protected virtual void OnDeviceExternalPowerChange(bool isConnectedToExternalPower) { }

	private bool HasSerialNumber => ConfigurationKey.UniqueId is { Length: not 0 };

	public string SerialNumber
		=> HasSerialNumber ?
			ConfigurationKey.UniqueId! :
			throw new NotSupportedException("This device does not support the Serial Number feature.");

	public DeviceId DeviceId => new(_deviceIdSource, VendorIdSource.Usb, RazerVendorId, _productId, _versionNumber);

	private class Generic : BaseDevice
	{
		public override DeviceCategory DeviceCategory { get; }

		private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

		public Generic(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			DeviceCategory deviceCategory,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			DeviceIdSource deviceIdSource,
			ushort productId,
			ushort versionNumber
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
		{
			DeviceCategory = deviceCategory;
			_allFeatures = FeatureCollection.CreateMerged(LightingFeatures, CreateBaseFeatures());
		}

		public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
	}

	private class Mouse :
		BaseDevice,
		IDeviceDriver<IMouseDeviceFeature>,
		IMouseDpiFeature,
		IMouseDynamicDpiFeature
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

		private uint _currentDpi;

		private readonly IDeviceFeatureCollection<IMouseDeviceFeature> _mouseFeatures;
		private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

		public Mouse(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			DeviceIdSource deviceIdSource,
			ushort productId,
			ushort versionNumber
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
		{
			var dpi = transport.GetDpi();
			_currentDpi = (uint)dpi.Vertical << 16 | dpi.Horizontal;
			_mouseFeatures = FeatureCollection.Create<IMouseDeviceFeature, Mouse, IMouseDpiFeature, IMouseDynamicDpiFeature>(this);
			_allFeatures = FeatureCollection.CreateMerged(LightingFeatures, _mouseFeatures, CreateBaseFeatures());
		}

		IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;
		public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

		protected override void OnDeviceDpiChange(ushort dpiX, ushort dpiY)
		{
			uint newDpi = (uint)dpiY << 16 | dpiX;
			uint oldDpi = Interlocked.Exchange(ref _currentDpi, newDpi);

			if (newDpi != oldDpi)
			{
				if (DpiChanged is { } dpiChanged)
				{
					_ = Task.Run
					(
						() =>
						{
							try
							{
								dpiChanged(this, GetDpi(newDpi));
							}
							catch (Exception ex)
							{
								// TODO: Log
							}
						}
					);
				}
			}
		}

		private static DotsPerInch GetDpi(uint rawValue)
			=> new((ushort)rawValue, (ushort)(rawValue >> 16));

		private event Action<Driver, DotsPerInch>? DpiChanged;

		DotsPerInch IMouseDpiFeature.CurrentDpi => GetDpi(Volatile.Read(ref _currentDpi));

		event Action<Driver, DotsPerInch> IMouseDynamicDpiFeature.DpiChanged
		{
			add => DpiChanged += value;
			remove => DpiChanged -= value;
		}
	}

	private class Keyboard : BaseDevice
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

		private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

		public Keyboard(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			DeviceIdSource deviceIdSource,
			ushort productId,
			ushort versionNumber
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
		{
			_allFeatures = FeatureCollection.CreateMerged(LightingFeatures, CreateBaseFeatures());
		}

		public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
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
				public ushort ProductId;

				public PairedDeviceState(ushort productId) : this() => ProductId = productId;
			}

			private readonly RazerDeviceNotificationWatcher _watcher;
			private readonly IDriverRegistry _driverRegistry;
			// As of now, there can be only two devices, but we can use an array here to be more future-proof. (Still need to understand how to address these other devices)
			private readonly PairedDeviceState[] _pairedDevices;

			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public ImmutableArray<string> DeviceNames { get; }

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;

			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

			public UsbReceiver(
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				IDriverRegistry driverRegistry,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				DeviceIdSource deviceIdSource,
				ushort productId,
				ushort versionNumber
			) : base(transport, periodicEventGenerator, friendlyName, configurationKey, deviceIdSource, productId, versionNumber)
			{
				_driverRegistry = driverRegistry;
				DeviceNames = deviceNames;
				_allFeatures = FeatureCollection.Create<IDeviceFeature, UsbReceiver, IDeviceIdFeature>(this);
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
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}

			protected override void OnDeviceArrival(byte deviceIndex)
			{
				// TODO: Log invalid device index.
				if (deviceIndex > _pairedDevices.Length) return;

				HandleNewDevice(deviceIndex);
			}

			protected override void OnDeviceRemoval(byte deviceIndex)
			{
				// TODO: Log invalid device index.
				if (deviceIndex > _pairedDevices.Length) return;

				if (Interlocked.Exchange(ref _pairedDevices[deviceIndex - 1].Driver, null) is { } oldDriver)
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
			{
				// Need to see how to handle devices other than the main one.
				if (deviceIndex != 1) return;

				ref var state = ref _pairedDevices[deviceIndex - 1];

				// Don't recreate a driver if one is already present.
				if (Volatile.Read(ref state.Driver) is not null) return;

				var basicDeviceInformation = _transport.GetDeviceInformation();

				// Update the state in case the paired device has changed.
				if (state.ProductId != basicDeviceInformation.ProductId)
				{
					state.ProductId = basicDeviceInformation.ProductId;
				}

				// If the device is already disconnected, skip everything else.
				if (!basicDeviceInformation.IsConnected) return;

				// TODO: Log unsupported device.
				if (!DeviceInformations.TryGetValue(basicDeviceInformation.ProductId, out var deviceInformation)) return;

				// Child devices would generally share a PID with their USB receiver, we need to get the information for the device and not the receiver.
				if (deviceInformation.ActualDeviceProductId != state.ProductId)
				{
					if (!DeviceInformations.TryGetValue(deviceInformation.ActualDeviceProductId, out deviceInformation)) return;
				}

				RazerDeviceDriver driver;

				try
				{
					var serialNumber = _transport.GetSerialNumber();

					driver = CreateChildDevice
					(
						_transport,
						_periodicEventGenerator,
						DeviceIdSource.Unknown,
						state.ProductId,
						0xFFFF,
						(byte)deviceIndex,
						deviceInformation,
						deviceInformation.FriendlyName,
						ConfigurationKey.DeviceMainId,
						serialNumber
					);
				}
				catch (Exception ex)
				{
					// TODO: Log exception

					return;
				}

				try
				{
					_driverRegistry.AddDriverAsync(driver);
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

					_driverRegistry.RemoveDriverAsync(oldDriver);
					DisposeDriver(oldDriver);
				}
			}

			protected override void OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY)
			{
				if (deviceIndex > _pairedDevices.Length) return;

				if (Volatile.Read(ref _pairedDevices[deviceIndex - 1].Driver) is { } driver)
				{
					driver.OnDeviceDpiChange(dpiX, dpiY);
				}
			}

			protected override void OnDeviceExternalPowerChange(byte deviceIndex, bool isCharging)
			{
				if (deviceIndex > _pairedDevices.Length) return;

				if (Volatile.Read(ref _pairedDevices[deviceIndex - 1].Driver) is { } driver)
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

		public class Generic : RazerDeviceDriver.Generic, ISystemDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public ImmutableArray<string> DeviceNames { get; }

			public Generic
			(
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				DeviceCategory deviceCategory,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceIdSource deviceIdSource,
				ushort productId,
				ushort versionNumber
			) : base(transport, periodicEventGenerator, deviceCategory, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}

		public class Mouse : RazerDeviceDriver.Mouse, ISystemDeviceDriver
		{
			private readonly RazerDeviceNotificationWatcher _watcher;

			public ImmutableArray<string> DeviceNames { get; }
			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			public Mouse
			(
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceIdSource deviceIdSource,
				ushort productId,
				ushort versionNumber
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
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
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				ImmutableArray<string> deviceNames,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				DeviceIdSource deviceIdSource,
				ushort productId,
				ushort versionNumber
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIdSource, productId, versionNumber)
			{
				_watcher = new(notificationStream, this);
				DeviceNames = deviceNames;
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				DisposeRootResources();
				await _watcher.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	private abstract class BaseDevice :
		RazerDeviceDriver,
		IDeviceDriver<ILightingDeviceFeature>,
		IBatteryStateDeviceFeature
	{
		private abstract class LightingZone :
			ILightingZone,
			ILightingZoneEffect<DisabledEffect>,
			ILightingDeferredChangesFeature,
			ILightingBrightnessFeature
		{
			protected BaseDevice Device { get; }

			public Guid ZoneId { get; }

			public LightingZone(BaseDevice device, Guid zoneId)
			{
				Device = device;
				ZoneId = zoneId;
			}

			public ILightingEffect GetCurrentEffect() => Device._currentEffect;

			void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => Device.SetCurrentEffect(DisabledEffect.SharedInstance);
			bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => Device._currentEffect.TryGetEffect(out effect);

			ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync() => Device.ApplyChangesAsync();

			byte ILightingBrightnessFeature.MaximumBrightness => 255;
			byte ILightingBrightnessFeature.CurrentBrightness
			{
				get => Device.CurrentBrightness;
				set => Device.CurrentBrightness = value;
			}
		}

		private class BasicLightingZone : LightingZone,
			ILightingZoneEffect<StaticColorEffect>,
			ILightingZoneEffect<ColorPulseEffect>,
			ILightingZoneEffect<TwoColorPulseEffect>,
			ILightingZoneEffect<RandomColorPulseEffect>,
			ILightingZoneEffect<ColorCycleEffect>,
			ILightingZoneEffect<ColorWaveEffect>
		{
			public BasicLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}

			void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(in ColorPulseEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<TwoColorPulseEffect>.ApplyEffect(in TwoColorPulseEffect effect) => Device.SetCurrentEffect(effect);
			void ILightingZoneEffect<RandomColorPulseEffect>.ApplyEffect(in RandomColorPulseEffect effect) => Device.SetCurrentEffect(RandomColorPulseEffect.SharedInstance);
			void ILightingZoneEffect<ColorCycleEffect>.ApplyEffect(in ColorCycleEffect effect) => Device.SetCurrentEffect(ColorCycleEffect.SharedInstance);
			void ILightingZoneEffect<ColorWaveEffect>.ApplyEffect(in ColorWaveEffect effect) => Device.SetCurrentEffect(ColorWaveEffect.SharedInstance);

			bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<TwoColorPulseEffect>.TryGetCurrentEffect(out TwoColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<RandomColorPulseEffect>.TryGetCurrentEffect(out RandomColorPulseEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<ColorCycleEffect>.TryGetCurrentEffect(out ColorCycleEffect effect) => Device._currentEffect.TryGetEffect(out effect);
			bool ILightingZoneEffect<ColorWaveEffect>.TryGetCurrentEffect(out ColorWaveEffect effect) => Device._currentEffect.TryGetEffect(out effect);
		}

		private class ReactiveLightingZone : BasicLightingZone, ILightingZoneEffect<ReactiveEffect>
		{
			public ReactiveLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}

			void ILightingZoneEffect<ReactiveEffect>.ApplyEffect(in ReactiveEffect effect) => Device.SetCurrentEffect(effect);
			bool ILightingZoneEffect<ReactiveEffect>.TryGetCurrentEffect(out ReactiveEffect effect) => Device._currentEffect.TryGetEffect(out effect);
		}

		private class UnifiedBasicLightingZone : BasicLightingZone, IUnifiedLightingFeature
		{
			public bool IsUnifiedLightingEnabled => true;

			public UnifiedBasicLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}
		}

		private class UnifiedReactiveLightingZone : ReactiveLightingZone, IUnifiedLightingFeature
		{
			public bool IsUnifiedLightingEnabled => true;

			public UnifiedReactiveLightingZone(BaseDevice device, Guid zoneId) : base(device, zoneId)
			{
			}
		}

		private ILightingEffect _appliedEffect;
		private ILightingEffect _currentEffect;
		private byte _appliedBrightness;
		private byte _currentBrightness;
		private readonly RazerDeviceFlags _deviceFlags;
		private readonly object _lightingLock;
		private readonly object _batteryStateLock;
		private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
		// How do we use this ?
		private readonly byte _deviceIndex;
		private ushort _batteryLevelAndChargingStatus;

		private bool HasBattery => (_deviceFlags & RazerDeviceFlags.HasBattery) != 0;
		private bool HasReactiveLighting => (_deviceFlags & RazerDeviceFlags.HasReactiveLighting) != 0;
		private bool IsWired => _deviceIdSource == DeviceIdSource.Usb;

		protected BaseDevice(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			DeviceIdSource deviceIdSource,
			ushort productId,
			ushort versionNumber
		) : base(transport, periodicEventGenerator, friendlyName, configurationKey, deviceIdSource, productId, versionNumber)
		{
			_appliedEffect = DisabledEffect.SharedInstance;
			_currentEffect = DisabledEffect.SharedInstance;
			_lightingLock = new();
			_batteryStateLock = new();
			_currentBrightness = 0x54; // 33%
			_deviceFlags = deviceFlags;

			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate(3, _transport.GetBatteryLevel(), _transport.IsConnectedToExternalPower());
				_periodicEventGenerator.Register(this);
			}

			_lightingFeatures = HasReactiveLighting ?
				FeatureCollection.Create<
					ILightingDeviceFeature,
					UnifiedReactiveLightingZone,
					ILightingDeferredChangesFeature,
					IUnifiedLightingFeature,
					ILightingBrightnessFeature>(new(this, lightingZoneId)) :
				FeatureCollection.Create<ILightingDeviceFeature,
					UnifiedBasicLightingZone,
					ILightingDeferredChangesFeature,
					IUnifiedLightingFeature,
					ILightingBrightnessFeature>(new(this, lightingZoneId));

			// No idea if that's the right thing to do but it seem to produce some valid good results. (Might just be by coincidence)
			byte flag = transport.GetDeviceInformationXXXXX();
			_appliedEffect = transport.GetSavedEffect(flag) ?? DisabledEffect.SharedInstance;

			// Reapply the persisted effect. (In case it was overridden by a temporary effect)
			ApplyEffect(_appliedEffect, _currentBrightness, false, true);

			_currentEffect = _appliedEffect;
		}

		public override ValueTask DisposeAsync()
		{
			if (HasBattery)
			{
				_periodicEventGenerator.Unregister(this);
			}
			return base.DisposeAsync();
		}

		protected IDeviceFeatureCollection<IDeviceFeature> CreateBaseFeatures()
			=> HasSerialNumber ?
				HasBattery ?
					FeatureCollection.Create<IDeviceFeature, BaseDevice, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
					FeatureCollection.Create<IDeviceFeature, BaseDevice, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
				HasBattery ?
					FeatureCollection.Create<IDeviceFeature, BaseDevice, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
					FeatureCollection.Create<IDeviceFeature, BaseDevice, IDeviceIdFeature>(this);

		protected override void HandlePeriodicEvent()
		{
			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate(3, _transport.GetBatteryLevel(), _transport.IsConnectedToExternalPower());
			}
		}

		protected override void OnDeviceExternalPowerChange(bool isCharging)
			=> ApplyBatteryLevelAndChargeStatusUpdate(2, 0, isCharging);

		private void ApplyBatteryLevelAndChargeStatusUpdate(byte changeType, byte newBatteryLevel, bool isCharging)
		{
			if (changeType == 0) return;

			lock (_batteryStateLock)
			{
				ushort oldBatteryLevelAndChargingStatus = _batteryLevelAndChargingStatus;
				ushort newBatteryLevelAndChargingStatus = 0;

				if ((changeType & 1) != 0)
				{
					newBatteryLevelAndChargingStatus = newBatteryLevel;
				}
				else
				{
					newBatteryLevelAndChargingStatus = (byte)oldBatteryLevelAndChargingStatus;
				}

				if ((changeType & 2) != 0)
				{
					newBatteryLevelAndChargingStatus = (ushort)(newBatteryLevelAndChargingStatus | (isCharging ? 0x100 : 0));
				}
				else
				{
					newBatteryLevelAndChargingStatus = (ushort)(newBatteryLevelAndChargingStatus | (oldBatteryLevelAndChargingStatus & 0xFF00));
				}

				if (oldBatteryLevelAndChargingStatus != newBatteryLevelAndChargingStatus)
				{
					Volatile.Write(ref _batteryLevelAndChargingStatus, newBatteryLevelAndChargingStatus);

					if (BatteryStateChanged is { } batteryStateChanged)
					{
						_ = Task.Run
						(
							() =>
							{
								try
								{
									batteryStateChanged.Invoke(this, BuildBatteryState(newBatteryLevelAndChargingStatus));
								}
								catch (Exception ex)
								{
									// TODO: Log
								}
							}
						);
					}
				}
			}
		}

		protected IDeviceFeatureCollection<ILightingDeviceFeature> LightingFeatures => _lightingFeatures;
		IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;

		private byte CurrentBrightness
		{
			get => Volatile.Read(ref _currentBrightness);
			set
			{
				lock (_lightingLock)
				{
					_currentBrightness = value;
				}
			}
		}

		private ValueTask ApplyChangesAsync()
		{
			lock (_lightingLock)
			{
				if (!ReferenceEquals(_appliedEffect, _currentEffect))
				{
					ApplyEffect(_currentEffect, _currentBrightness, false, _appliedEffect is DisabledEffect || _appliedBrightness != _currentBrightness);
					_appliedEffect = _currentEffect;
				}
				else if (!ReferenceEquals(_currentEffect, DisabledEffect.SharedInstance) && _appliedBrightness != _currentBrightness)
				{
					_transport.SetBrightness(false, _currentBrightness);
				}
				_appliedBrightness = _currentBrightness;
			}
			return ValueTask.CompletedTask;
		}

		private void ApplyEffect(ILightingEffect effect, byte brightness, bool shouldPersist, bool forceBrightnessUpdate)
		{
			if (ReferenceEquals(effect, DisabledEffect.SharedInstance))
			{
				_transport.SetEffect(shouldPersist, 0, 0, default, default);
				_transport.SetBrightness(shouldPersist, 0);
				return;
			}

			// It seems brightness must be restored from zero first before setting a color effect.
			// Otherwise, the device might restore to its saved effect. (e.g. Color Cycle)
			if (forceBrightnessUpdate)
			{
				_transport.SetBrightness(shouldPersist, brightness);
			}

			switch (effect)
			{
			case StaticColorEffect staticColorEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Static, 1, staticColorEffect.Color, staticColorEffect.Color);
				break;
			case RandomColorPulseEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Breathing, 0, default, default);
				break;
			case ColorPulseEffect colorPulseEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Breathing, 1, colorPulseEffect.Color, default);
				break;
			case TwoColorPulseEffect twoColorPulseEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Breathing, 2, twoColorPulseEffect.Color, twoColorPulseEffect.SecondColor);
				break;
			case ColorCycleEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.SpectrumCycle, 0, default, default);
				break;
			case ColorWaveEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Wave, 0, default, default);
				break;
			case ReactiveEffect reactiveEffect:
				_transport.SetEffect(shouldPersist, RazerLightingEffect.Reactive, 1, reactiveEffect.Color, default);
				break;
			}
		}

		private void SetCurrentEffect(ILightingEffect effect)
		{
			lock (_lightingLock)
			{
				_currentEffect = effect;
			}
		}

		// TODO: Determine how this should be exposed.
		public void SetDefaultBrightness(byte brightness)
		{
			_currentBrightness = brightness;
		}

		private event Action<Driver, BatteryState> BatteryStateChanged;

		event Action<Driver, BatteryState> IBatteryStateDeviceFeature.BatteryStateChanged
		{
			add => BatteryStateChanged += value;
			remove => BatteryStateChanged -= value;
		}

		BatteryState IBatteryStateDeviceFeature.BatteryState
			=> BuildBatteryState(Volatile.Read(ref _batteryLevelAndChargingStatus));

		private BatteryState BuildBatteryState(ushort rawBatteryLevelAndChargingStatus)
		{
			// Reasoning:
			// If the device is directly connected to USB, it is not in wireless mode.
			// If the device is wireless, it is discharging. Otherwise, it is charging up to 100%.
			// This is based on the current state of things. It could change depending on the technical possibilities.
			bool isWired = IsWired;

			bool isCharging = (rawBatteryLevelAndChargingStatus & 0x100) != 0;
			byte batteryLevel = (byte)rawBatteryLevelAndChargingStatus;

			return new()
			{
				Level = batteryLevel / 255f,
				BatteryStatus = isWired || isCharging ?
					batteryLevel == 255 ? BatteryStatus.ChargingComplete : BatteryStatus.Charging :
					BatteryStatus.Discharging,
				// NB: What meaning should we put behind external power ? If the mouse is on a dock it technically has external power, but it is not usable…
				ExternalPowerStatus = isWired || isCharging ? ExternalPowerStatus.IsConnected : ExternalPowerStatus.IsDisconnected,
			};
		}
	}
}
