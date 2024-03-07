using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Razer.LightingEffects;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Features.MouseFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Razer;

// Like the Logitech driver, this will likely benefit from a refactoring of the device discovery, allowing to create drivers with different features on-demand.
// For now, it will only exactly support the features for Razer DeathAdder V2 & Dock, but it will need more flexibility to support other devices using the same protocol.
// NB: This driver relies on system drivers provided by Razer to access device features. The protocol part is still implemented here, but we need the driver to get access to the device.
public abstract class RazerDeviceDriver :
	Driver,
	IDeviceDriver<IBaseDeviceFeature>,
	IRazerDeviceNotificationSink,
	IRazerPeriodicEventHandler,
	IDeviceSerialNumberFeature,
	IDeviceIdFeature,
	IDeviceIdsFeature
{
	private const ushort RazerVendorId = 0x1532;

	// It does not seem we can retrieve enough metadata from the devices themselves, so we need to have some manually entered data here.
	private readonly struct DeviceInformation
	{
		public DeviceInformation
		(
			RazerDeviceCategory deviceCategory,
			RazerDeviceFlags flags,
			ushort wiredDeviceProductId,
			ushort dongleDeviceProductId,
			ushort bluetoothDeviceProductId,
			ushort bluetoothLowEnergyDeviceProductId,
			Guid? lightingZoneGuid,
			string friendlyName
		)
		{
			DeviceCategory = deviceCategory;
			Flags = flags;
			WiredDeviceProductId = wiredDeviceProductId;
			DongleDeviceProductId = dongleDeviceProductId;
			BluetoothDeviceProductId = bluetoothDeviceProductId;
			BluetoothLowEnergyDeviceProductId = bluetoothLowEnergyDeviceProductId;
			LightingZoneGuid = lightingZoneGuid;
			FriendlyName = friendlyName;
		}

		public RazerDeviceCategory DeviceCategory { get; }

		public RazerDeviceFlags Flags { get; }

		// These properties indicate the various IDs related to a device. Availability of these IDs is indicated by the flags.
		// The meaning slightly changes if the device is a dongle.
		// In the case of a dongle, only the DongleDeviceProductId corresponds to the actual dongle device ID. And it must be available.
		// The other available product IDs always correspond to the product IDs of a device.
		// When connected through a dongle, the device will report its ID as the one of the dongle it came with.
		// e.g. DeathAdder V2 Pro is 007C (wired) and 008E (BT). Connected to a dongle, it is referenced as 007D, which is the ID of the dongle it is delivered with.

		public ushort WiredDeviceProductId { get; }
		public ushort DongleDeviceProductId { get; }
		public ushort BluetoothDeviceProductId { get; }
		public ushort BluetoothLowEnergyDeviceProductId { get; }

		public Guid? LightingZoneGuid { get; }
		public string FriendlyName { get; }

		public bool HasWiredDeviceProductId => (Flags & RazerDeviceFlags.HasWiredProductId) != 0;
		public bool HasDongleProductId => (Flags & RazerDeviceFlags.HasDongleProductId) != 0;
		public bool HasBluetoothProductId => (Flags & RazerDeviceFlags.HasBluetoothProductId) != 0;
		public bool HasBluetoothLowEnergyProductId => (Flags & RazerDeviceFlags.HasBluetoothLowEnergyProductId) != 0;

		public bool IsDongle => (Flags & RazerDeviceFlags.IsDongle) != 0;

		public ushort GetMainProductId()
			=> (Flags & (RazerDeviceFlags.HasDongleProductId | RazerDeviceFlags.IsDongle)) == (RazerDeviceFlags.HasDongleProductId | RazerDeviceFlags.IsDongle) ?
				DongleDeviceProductId :
				(Flags & RazerDeviceFlags.HasWiredProductId) == 0 ?
					(Flags & RazerDeviceFlags.HasBluetoothLowEnergyProductId) == 0 ?
						(Flags & RazerDeviceFlags.HasBluetoothProductId) == 0 ?
							(ushort)0 :
							BluetoothDeviceProductId :
						BluetoothLowEnergyDeviceProductId :
					WiredDeviceProductId;

		public ImmutableArray<DeviceId> GetDeviceIds(ushort versionNumber)
		{
			if (IsDongle) return ImmutableArray.Create(new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DongleDeviceProductId, versionNumber));

			const RazerDeviceFlags ProductIdFlags = RazerDeviceFlags.HasWiredProductId |
				RazerDeviceFlags.HasDongleProductId |
				RazerDeviceFlags.HasBluetoothProductId |
				RazerDeviceFlags.HasBluetoothLowEnergyProductId;
			int count = BitOperations.PopCount((byte)(Flags & ProductIdFlags));
			if (count == 0) return ImmutableArray<DeviceId>.Empty;

			var productIds = new DeviceId[count];
			int index = 0;
			if (HasWiredDeviceProductId) productIds[index++] = new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, WiredDeviceProductId, versionNumber);
			if (HasDongleProductId) productIds[index++] = new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DongleDeviceProductId, versionNumber);
			if (HasBluetoothProductId) productIds[index++] = new DeviceId(DeviceIdSource.Bluetooth, VendorIdSource.Usb, RazerVendorId, BluetoothDeviceProductId, versionNumber);
			if (HasBluetoothLowEnergyProductId) productIds[index++] = new DeviceId(DeviceIdSource.BluetoothLowEnergy, VendorIdSource.Usb, RazerVendorId, BluetoothLowEnergyDeviceProductId, versionNumber);

			return Unsafe.As<DeviceId[], ImmutableArray<DeviceId>>(ref productIds);
		}
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

		HasReactiveLighting = 0x02,

		HasWiredProductId = 0x08,
		HasDongleProductId = 0x10,
		HasBluetoothProductId = 0x20,
		HasBluetoothLowEnergyProductId = 0x40,

		IsDongle = 0x80,
	}

	private static readonly Guid RazerControlDeviceInterfaceClassGuid = new(0xe3be005d, 0xd130, 0x4910, 0x88, 0xff, 0x09, 0xae, 0x02, 0xf6, 0x80, 0xe9);

	private static readonly Guid DockLightingZoneGuid = new(0x5E410069, 0x0F34, 0x4DD8, 0x80, 0xDB, 0x5B, 0x11, 0xFB, 0xD4, 0x13, 0xD6);
	private static readonly Guid DeathAdderV2ProLightingZoneGuid = new(0x4D2EE313, 0xEA46, 0x4857, 0x89, 0x8C, 0x5B, 0xF9, 0x44, 0x09, 0x0A, 0x9A);

	// Stores the device informations in a linear table that allow deduplicating information and accessing it by reference.
	// The indices into this table will be built into the dictionary.
	// NB: Accessing by reference could also be done with the unsafe dictionary APIs, but not deduplicating the data.
	// TODO: Make a source generator for this and store everything in a data block using ReadOnlySpan<byte>.
	private static readonly DeviceInformation[] DeviceInformationTable =
	[
		new
		(
			RazerDeviceCategory.Mouse,
			RazerDeviceFlags.HasBattery | RazerDeviceFlags.HasReactiveLighting | RazerDeviceFlags.HasWiredProductId | RazerDeviceFlags.HasDongleProductId | RazerDeviceFlags.HasBluetoothLowEnergyProductId,
			0x007C,
			0x007D,
			0xFFFF,
			0x008E,
			DeathAdderV2ProLightingZoneGuid,
			"Razer DeathAdder V2 Pro"
		),
		new
		(
			RazerDeviceCategory.UsbReceiver,
			RazerDeviceFlags.HasBattery |
				RazerDeviceFlags.HasReactiveLighting |
				RazerDeviceFlags.HasWiredProductId |
				RazerDeviceFlags.HasDongleProductId |
				RazerDeviceFlags.HasBluetoothLowEnergyProductId |
				RazerDeviceFlags.IsDongle,
			0x007C,
			0x007D,
			0xFFFF,
			0x008E,
			null,
			"Razer DeathAdder V2 Pro HyperSpeed Dongle"
		),
		new
		(
			RazerDeviceCategory.Dock,
			RazerDeviceFlags.HasWiredProductId,
			0x007E,
			0xFFFF,
			0xFFFF,
			0xFFFF,
			DockLightingZoneGuid,
			"Razer Mouse Dock"
		)
	];

	private static readonly Dictionary<ushort, ushort> DeviceInformationIndices = new()
	{
		{ 0x007C, 0 },
		{ 0x007D, 1 },
		{ 0x007E, 2 },
		{ 0x008E, 0 },
	};

	private static ref readonly DeviceInformation GetDeviceInformation(ushort productId)
	{
		if (DeviceInformationIndices.TryGetValue(productId, out ushort index))
		{
			return ref DeviceInformationTable[index];
		}
		return ref Unsafe.NullRef<DeviceInformation>();
	}

	private static byte GetDeviceIdIndex(ImmutableArray<DeviceId> deviceIds, ushort productId)
	{
		for (int i = 0; i < deviceIds.Length; i++)
		{
			if (deviceIds[i].ProductId == productId) return (byte)i;
		}
		throw new InvalidOperationException("Could not find a matching device ID entry.");
	}

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007C)] // DeathAdder V2 Pro Mouse Wired
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007D)] // DeathAdder V2 Pro Mouse via Dongle
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007E)] // DeathAdder V2 Pro Dock
	//[ProductId(VendorIdSource.Usb, RazerVendorId, 0x008E)] // DeathAdder V2 Pro Mouse BLE
	public static async Task<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		string topLevelDeviceName,
		Optional<IDriverRegistry> driverRegistry,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken
	)
	{
		// Start by retrieving the local device metadata. It will throw if missing, which is what we want.
		// We need this for two reasons:
		// 1 - This is less than ideal, but it seems we can't retrieve enough information from the device themselves (type of device, etc.)
		// 2 - We need predefined lighting zone GUIDs. (Maybe we could generate these from VID/PID pairs to avoid this manual mapping ?)
		var deviceInfo = GetDeviceInformation(productId);

		string? razerControlDeviceName = null;
		string? notificationDeviceName = null;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == RazerControlDeviceInterfaceClassGuid)
			{
				if (razerControlDeviceName is not null)
				{
					throw new InvalidOperationException("Expected a single device interface for Razer device control.");
				}

				razerControlDeviceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
					usagePage == (ushort)HidUsagePage.GenericDesktop && usageId == 0)
				{
					using (var deviceHandle = HidDevice.FromPath(deviceInterface.Id))
					{
						// TODO: Might finally be time to work on that HID descriptor part 😫
						// Also, I don't know why Windows insist on declaring the values we are looking for as button. The HID descriptor clearly indicates values between 0 and 255…
						var descriptor = await deviceHandle.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);

						// Thanks to the button caps we can check the Report ID.
						// We are looking for the HID device interface tied to the collection with Report ID 5.
						// (Remember this relatively annoying Windows-specific stuff of splitting interfaces by top-level collection)
						if (descriptor.InputReports[0].ReportId == 5)
						{
							if (notificationDeviceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device notifications.");

							notificationDeviceName = deviceInterface.Id;
						}
					}
				}
			}
		}

		if (razerControlDeviceName is null)
		{
			throw new InvalidOperationException("The device interface for Razer device control was not found.");
		}

		if (notificationDeviceName is null)
		{
			throw new InvalidOperationException("The device interface for device notifications was not found.");
		}

		var transport = new RazerProtocolTransport(new DeviceStream(Device.OpenHandle(razerControlDeviceName, DeviceAccess.None), FileAccess.ReadWrite));

		string? serialNumber = null;

		if (deviceInfo.DeviceCategory != RazerDeviceCategory.UsbReceiver)
		{
			serialNumber = await transport.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
		}

		var device = await CreateDeviceAsync
		(
			transport,
			new(60_000),
			new(notificationDeviceName),
			driverRegistry,
			version,
			deviceInfo,
			friendlyName,
			topLevelDeviceName,
			serialNumber,
			cancellationToken
		).ConfigureAwait(false);

		// Get rid of the nested driver registry if we don't need it.
		if (device is not SystemDevice.UsbReceiver)
		{
			driverRegistry.Dispose();
		}

		return new DriverCreationResult<SystemDevicePath>(keys, device, null);
	}

	private static async ValueTask<RazerDeviceDriver> CreateDeviceAsync
	(
		RazerProtocolTransport transport,
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		HidFullDuplexStream notificationStream,
		Optional<IDriverRegistry> driverRegistry,
		ushort versionNumber,
		DeviceInformation deviceInfo,
		string friendlyName,
		string topLevelDeviceName,
		string? serialNumber,
		CancellationToken cancellationToken
	)
	{
		// Determine the main device ID using priority rules.
		ushort mainProductId = deviceInfo.GetMainProductId();
		var deviceIds = deviceInfo.GetDeviceIds(versionNumber);
		byte mainDeviceIdIndex = GetDeviceIdIndex(deviceIds, mainProductId);

		var configurationKey = new DeviceConfigurationKey("RazerDevice", topLevelDeviceName, $"{RazerVendorId:X4}:{mainProductId:X4}", serialNumber);

		RazerDeviceDriver driver = deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new SystemDevice.Keyboard
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Mouse => new SystemDevice.Mouse
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Dock => new SystemDevice.Generic
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				DeviceCategory.MouseDock,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.UsbReceiver => new SystemDevice.UsbReceiver
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				driverRegistry.GetOrCreateValue(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceIds,
				mainDeviceIdIndex
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
		try
		{
			await driver.InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await driver.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return driver;
	}

	private static async ValueTask<RazerDeviceDriver> CreateChildDeviceAsync
	(
		RazerProtocolTransport transport,
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		DeviceIdSource deviceIdSource,
		ushort versionNumber,
		byte deviceIndex,
		DeviceInformation deviceInfo,
		string friendlyName,
		string topLevelDeviceName,
		string serialNumber,
		CancellationToken cancellationToken
	)
	{
		// Determine the main device ID using priority rules.
		ushort mainProductId = deviceInfo.GetMainProductId();
		var deviceIds = deviceInfo.GetDeviceIds(versionNumber);
		byte mainDeviceIdIndex = GetDeviceIdIndex(deviceIds, mainProductId);

		var configurationKey = new DeviceConfigurationKey
		(
			"RazerDevice",
			$"{topLevelDeviceName}#IX_{deviceIndex:X2}&PID_{mainProductId:X4}",
			$"{RazerVendorId:X4}:{mainProductId:X4}",
			serialNumber
		);

		RazerDeviceDriver driver = deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new Keyboard
			(
				transport,
				periodicEventGenerator,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Mouse => new Mouse
			(
				transport,
				periodicEventGenerator,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
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
				deviceIds,
				mainDeviceIdIndex
			),
			_ => throw new InvalidOperationException("Unsupported device."),
		};
		try
		{
			await driver.InitializeAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			await driver.DisposeAsync().ConfigureAwait(false);
			throw;
		}
		return driver;
	}

	// The transport is used to communicate with the device.
	private readonly RazerProtocolTransport _transport;

	// The periodic event generator is used to manage periodic events on the transport.
	// It *could* be merged with the transport for practical reasons but the two features are not related enough.
	private readonly RazerProtocolPeriodicEventGenerator _periodicEventGenerator;

	private readonly DeviceIdSource _deviceIdSource;
	private readonly ImmutableArray<DeviceId> _deviceIds;
	private readonly byte _mainDeviceIdIndex;
	private readonly RazerDeviceFlags _deviceFlags;

	private readonly IDeviceFeatureCollection<IBaseDeviceFeature> _baseFeatures;

	IDeviceFeatureCollection<IBaseDeviceFeature> IDeviceDriver<IBaseDeviceFeature>.Features => _baseFeatures;

	private bool HasSerialNumber => ConfigurationKey.UniqueId is { Length: not 0 };

	string IDeviceSerialNumberFeature.SerialNumber
		=> HasSerialNumber ?
			ConfigurationKey.UniqueId! :
			throw new NotSupportedException("This device does not support the Serial Number feature.");

	DeviceId IDeviceIdFeature.DeviceId => _deviceIds[_mainDeviceIdIndex];

	ImmutableArray<DeviceId> IDeviceIdsFeature.DeviceIds => _deviceIds;
	int? IDeviceIdsFeature.MainDeviceIdIndex => _mainDeviceIdIndex;

	private RazerDeviceDriver
	(
		RazerProtocolTransport transport,
		RazerProtocolPeriodicEventGenerator periodicEventGenerator,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		ImmutableArray<DeviceId> deviceIds,
		byte mainDeviceIdIndex,
		RazerDeviceFlags deviceFlags
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_periodicEventGenerator = periodicEventGenerator;
		_deviceIds = deviceIds;
		_mainDeviceIdIndex = mainDeviceIdIndex;
		_deviceFlags = deviceFlags;
		_baseFeatures = CreateBaseFeatures();
	}

	protected virtual ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

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

	protected virtual IDeviceFeatureCollection<IBaseDeviceFeature> CreateBaseFeatures()
		=> HasSerialNumber ?
			FeatureCollection.Create<IBaseDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature, IDeviceSerialNumberFeature>(this) :
			FeatureCollection.Create<IBaseDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature>(this);

	async void IRazerPeriodicEventHandler.HandlePeriodicEvent()
	{
		try
		{
			await HandlePeriodicEventAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			// TODO: Log
		}
	}

	protected virtual ValueTask HandlePeriodicEventAsync() => ValueTask.CompletedTask;

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

	private class Generic : BaseDevice
	{
		public override DeviceCategory DeviceCategory { get; }

		public Generic(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			DeviceCategory deviceCategory,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			DeviceCategory = deviceCategory;
		}
	}

	private class Mouse :
		BaseDevice,
		IDeviceDriver<IMouseDeviceFeature>,
		IMouseDpiFeature,
		IMouseDynamicDpiFeature,
		IMouseDpiPresetFeature
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

		private DotsPerInch[] _dpiProfiles;
		private ulong _currentDpi;

		private readonly IDeviceFeatureCollection<IMouseDeviceFeature> _mouseFeatures;

		public Mouse(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_dpiProfiles = [];
			_mouseFeatures = FeatureCollection.Create<IMouseDeviceFeature, Mouse, IMouseDpiFeature, IMouseDynamicDpiFeature, IMouseDpiPresetFeature>(this);
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);

			var dpiLevels = await _transport.GetDpiProfilesAsync(false, cancellationToken).ConfigureAwait(false);
			_dpiProfiles = Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(dpiLevels.Profiles)!, p => new DotsPerInch(p.X, p.Y));
			var dpi = await _transport.GetDpiAsync(cancellationToken).ConfigureAwait(false);
			_currentDpi = (uint)dpi.Vertical << 16 | dpi.Horizontal;
		}

		IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => _mouseFeatures;

		protected override void OnDeviceDpiChange(ushort dpiX, ushort dpiY)
		{
			var profiles = Volatile.Read(ref _dpiProfiles);

			uint profileIndex = 0;
			for (int i = 0; i < profiles.Length; i++)
			{
				if (profiles[i].Horizontal == dpiX && profiles[i].Vertical == dpiY)
				{
					profileIndex = (uint)i + 1;
					break;
				}
			}

			ulong newDpi = (ulong)profileIndex << 32 | (uint)dpiY << 16 | dpiX;
			ulong oldDpi = Interlocked.Exchange(ref _currentDpi, newDpi);

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

		private static MouseDpiStatus GetDpi(ulong rawValue)
			=> new()
			{
				PresetIndex = (byte)(rawValue >> 32) is > 0 and byte i ? i : null,
				Dpi = new((ushort)rawValue, (ushort)(rawValue >> 16))
			};

		private event Action<Driver, MouseDpiStatus>? DpiChanged;

		MouseDpiStatus IMouseDpiFeature.CurrentDpi => GetDpi(Volatile.Read(ref _currentDpi));

		event Action<Driver, MouseDpiStatus> IMouseDynamicDpiFeature.DpiChanged
		{
			add => DpiChanged += value;
			remove => DpiChanged -= value;
		}

		ImmutableArray<DotsPerInch> IMouseDpiPresetFeature.DpiPresets => ImmutableCollectionsMarshal.AsImmutableArray(Volatile.Read(ref _dpiProfiles));
	}

	private class Keyboard : BaseDevice
	{
		public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

		public Keyboard(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			RazerDeviceFlags deviceFlags,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex
		) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
		}
	}

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
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				IDriverRegistry driverRegistry,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, RazerDeviceFlags.None)
			{
				_driverRegistry = driverRegistry;
				_watcher = new(notificationStream, this);
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
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				DeviceCategory deviceCategory,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, deviceCategory, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this);
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
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this);
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
				RazerProtocolTransport transport,
				RazerProtocolPeriodicEventGenerator periodicEventGenerator,
				HidFullDuplexStream notificationStream,
				Guid lightingZoneId,
				string friendlyName,
				DeviceConfigurationKey configurationKey,
				RazerDeviceFlags deviceFlags,
				ImmutableArray<DeviceId> deviceIds,
				byte mainDeviceIdIndex
			) : base(transport, periodicEventGenerator, lightingZoneId, friendlyName, configurationKey, deviceFlags, deviceIds, mainDeviceIdIndex)
			{
				_watcher = new(notificationStream, this);
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

			ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync() => Device.ApplyChangesAsync(default);

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
		private readonly AsyncLock _lightingLock;
		private readonly AsyncLock _batteryStateLock;
		private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
		// How do we use this ?
		private readonly byte _deviceIndex;
		private ushort _batteryLevelAndChargingStatus;

		private bool HasBattery => (_deviceFlags & RazerDeviceFlags.HasBattery) != 0;
		private bool HasReactiveLighting => (_deviceFlags & RazerDeviceFlags.HasReactiveLighting) != 0;
		private bool IsWired => _deviceIdSource == DeviceIdSource.Usb;

		protected BaseDevice
		(
			RazerProtocolTransport transport,
			RazerProtocolPeriodicEventGenerator periodicEventGenerator,
			Guid lightingZoneId,
			string friendlyName,
			DeviceConfigurationKey configurationKey,
			ImmutableArray<DeviceId> deviceIds,
			byte mainDeviceIdIndex,
			RazerDeviceFlags deviceFlags
		) : base(transport, periodicEventGenerator, friendlyName, configurationKey, deviceIds, mainDeviceIdIndex, deviceFlags)
		{
			_appliedEffect = DisabledEffect.SharedInstance;
			_currentEffect = DisabledEffect.SharedInstance;
			_lightingLock = new();
			_batteryStateLock = new();
			_currentBrightness = 0x54; // 33%

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
		}

		protected override async ValueTask InitializeAsync(CancellationToken cancellationToken)
		{
			await base.InitializeAsync(cancellationToken).ConfigureAwait(false);
			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate
				(
					3,
					await _transport.GetBatteryLevelAsync(cancellationToken).ConfigureAwait(false),
					await _transport.IsConnectedToExternalPowerAsync(cancellationToken).ConfigureAwait(false)
				);
				_periodicEventGenerator.Register(this);
			}

			// No idea if that's the right thing to do but it seem to produce some valid good results. (Might just be by coincidence)
			byte flag = await _transport.GetDeviceInformationXxxxxAsync(cancellationToken).ConfigureAwait(false);
			_appliedEffect = await _transport.GetSavedEffectAsync(flag, cancellationToken).ConfigureAwait(false) ?? DisabledEffect.SharedInstance;

			// Reapply the persisted effect. (In case it was overridden by a temporary effect)
			await ApplyEffectAsync(_appliedEffect, _currentBrightness, false, true, cancellationToken).ConfigureAwait(false);

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

		protected override IDeviceFeatureCollection<IBaseDeviceFeature> CreateBaseFeatures()
			=> HasSerialNumber ?
				HasBattery ?
					FeatureCollection.Create<IBaseDeviceFeature, BaseDevice, IDeviceIdFeature, IDeviceSerialNumberFeature, IBatteryStateDeviceFeature>(this) :
					FeatureCollection.Create<IBaseDeviceFeature, BaseDevice, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
				HasBattery ?
					FeatureCollection.Create<IBaseDeviceFeature, BaseDevice, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
					FeatureCollection.Create<IBaseDeviceFeature, BaseDevice, IDeviceIdFeature>(this);

		protected override async ValueTask HandlePeriodicEventAsync()
		{
			if (HasBattery)
			{
				ApplyBatteryLevelAndChargeStatusUpdate
				(
					3,
					await _transport.GetBatteryLevelAsync(default).ConfigureAwait(false),
					await _transport.IsConnectedToExternalPowerAsync(default).ConfigureAwait(false)
				);
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

		private async ValueTask ApplyChangesAsync(CancellationToken cancellationToken)
		{
			using (await _lightingLock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!ReferenceEquals(_appliedEffect, _currentEffect))
				{
					await ApplyEffectAsync(_currentEffect, _currentBrightness, false, _appliedEffect is DisabledEffect || _appliedBrightness != _currentBrightness, cancellationToken).ConfigureAwait(false);
					_appliedEffect = _currentEffect;
				}
				else if (!ReferenceEquals(_currentEffect, DisabledEffect.SharedInstance) && _appliedBrightness != _currentBrightness)
				{
					await _transport.SetBrightnessAsync(false, _currentBrightness, cancellationToken).ConfigureAwait(false);
				}
				_appliedBrightness = _currentBrightness;
			}
		}

		private async ValueTask ApplyEffectAsync(ILightingEffect effect, byte brightness, bool shouldPersist, bool forceBrightnessUpdate, CancellationToken cancellationToken)
		{
			if (ReferenceEquals(effect, DisabledEffect.SharedInstance))
			{
				await _transport.SetEffectAsync(shouldPersist, 0, 0, default, default, cancellationToken).ConfigureAwait(false);
				await _transport.SetBrightnessAsync(shouldPersist, 0, cancellationToken);
				return;
			}

			// It seems brightness must be restored from zero first before setting a color effect.
			// Otherwise, the device might restore to its saved effect. (e.g. Color Cycle)
			if (forceBrightnessUpdate)
			{
				await _transport.SetBrightnessAsync(shouldPersist, brightness, cancellationToken);
			}

			switch (effect)
			{
			case StaticColorEffect staticColorEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Static, 1, staticColorEffect.Color, staticColorEffect.Color, cancellationToken);
				break;
			case RandomColorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 0, default, default, cancellationToken);
				break;
			case ColorPulseEffect colorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 1, colorPulseEffect.Color, default, cancellationToken);
				break;
			case TwoColorPulseEffect twoColorPulseEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Breathing, 2, twoColorPulseEffect.Color, twoColorPulseEffect.SecondColor, cancellationToken);
				break;
			case ColorCycleEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.SpectrumCycle, 0, default, default, cancellationToken);
				break;
			case ColorWaveEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Wave, 0, default, default, cancellationToken);
				break;
			case ReactiveEffect reactiveEffect:
				await _transport.SetEffectAsync(shouldPersist, RazerLightingEffect.Reactive, 1, reactiveEffect.Color, default, cancellationToken);
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

		private event Action<Driver, BatteryState>? BatteryStateChanged;

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
