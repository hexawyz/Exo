using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Razer;

// Like the Logitech driver, this will likely benefit from a refactoring of the device discovery, allowing to create drivers with different features on-demand.
// For now, it will only exactly support the features for Razer DeathAdder V2 & Dock, but it will need more flexibility to support other devices using the same protocol.
// NB: This driver relies on system drivers provided by Razer to access device features. The protocol part is still implemented here, but we need the driver to get access to the device.
public abstract partial class RazerDeviceDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IRazerDeviceNotificationSink,
	IDeviceSerialNumberFeature,
	IDeviceIdFeature,
	IDeviceIdsFeature
{
	private const ushort RazerVendorId = 0x1532;

	private const ushort MaximumPollingFrequency = 1000;

	private static readonly ImmutableArray<byte> SupportedPollingFrequencyDividers = [1, 2, 8];

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
			ushort maximumDpi,
			string friendlyName
		)
		{
			DeviceCategory = deviceCategory;
			Flags = flags;
			MaximumDpi = maximumDpi;
			WiredDeviceProductId = wiredDeviceProductId;
			DongleDeviceProductId = dongleDeviceProductId;
			BluetoothDeviceProductId = bluetoothDeviceProductId;
			BluetoothLowEnergyDeviceProductId = bluetoothLowEnergyDeviceProductId;
			LightingZoneGuid = lightingZoneGuid;
			FriendlyName = friendlyName;
		}

		public RazerDeviceCategory DeviceCategory { get; }

		public RazerDeviceFlags Flags { get; }

		public ushort MaximumDpi { get; }

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
			if (IsDongle) return [new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DongleDeviceProductId, versionNumber)];

			const RazerDeviceFlags ProductIdFlags = RazerDeviceFlags.HasWiredProductId |
				RazerDeviceFlags.HasDongleProductId |
				RazerDeviceFlags.HasBluetoothProductId |
				RazerDeviceFlags.HasBluetoothLowEnergyProductId;
			int count = BitOperations.PopCount((byte)(Flags & ProductIdFlags));
			if (count == 0) return [];

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

	// This is the GUID of the BLE GATT service used by Razer DeathAdder V2 Pro and possibly other devices.
	// The BLE protocol is different from the USB HID protocol and does not require a kernel driver. Instead, it uses a custom BLE service exposed by the device.
	private static readonly Guid RazerGattServiceGuid = new(0x52401523, 0xF97C, 0x7F90, 0x0E, 0x7F, 0x6C, 0x6F, 0x4E, 0x36, 0xDB, 0x1C);

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
			RazerDeviceFlags.HasBattery |
				RazerDeviceFlags.HasReactiveLighting |
				RazerDeviceFlags.HasWiredProductId |
				RazerDeviceFlags.HasDongleProductId |
				RazerDeviceFlags.HasBluetoothLowEnergyProductId,
			0x007C,
			0x007D,
			0xFFFF,
			0x008E,
			DeathAdderV2ProLightingZoneGuid,
			20_000,
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
			0,
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
			0,
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
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x008E)] // DeathAdder V2 Pro Mouse BLE
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

		string? razerControlDeviceInterfaceName = null;
		string? razerFeatureReportDeviceInterfaceName = null;
		string? notificationDeviceInterfaceName = null;
		string? secondaryNotificationDeviceInterfaceName = null;
		string? razerGattServiceDeviceInterfaceName = null;
		byte notificationReportLength = 0;
		byte secondaryNotificationReportLength = 0;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid))
			{
				continue;
			}

			if (interfaceClassGuid == RazerControlDeviceInterfaceClassGuid)
			{
				if (razerControlDeviceInterfaceName is not null)
				{
					throw new InvalidOperationException("Expected a single device interface for Razer device control.");
				}

				razerControlDeviceInterfaceName = deviceInterface.Id;
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.Hid)
			{
				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
					(HidUsagePage)usagePage == HidUsagePage.GenericDesktop)
				{
					switch ((HidGenericDesktopUsage)usageId)
					{
					case 0:
						using (var deviceHandle = HidDevice.FromPath(deviceInterface.Id))
						{
							var descriptor = await deviceHandle.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);

							// We are looking for the HID device interface tied to the collection with Report ID 5.
							// This device interface will be the one providing us with the most useful notifications. Other interfaces can also provide some notifications but their purpose is unknown.
							// (NB: Remember this relatively annoying Windows-specific stuff of splitting interfaces by top-level collection)
							if (descriptor.InputReports.Count == 1)
							{
								switch (descriptor.InputReports[0].ReportId)
								{
								case 0x05:
									if (notificationDeviceInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device notifications.");

									notificationDeviceInterfaceName = deviceInterface.Id;
									notificationReportLength = checked((byte)descriptor.InputReports[0].ReportSize);
									break;
								case 0x09:
									if (secondaryNotificationDeviceInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device notifications second channel.");

									secondaryNotificationDeviceInterfaceName = deviceInterface.Id;
									secondaryNotificationReportLength = checked((byte)descriptor.InputReports[0].ReportSize);
									break;
								}
							}
						}
						break;
					case HidGenericDesktopUsage.Mouse:
						using (var deviceHandle = HidDevice.FromPath(deviceInterface.Id))
						{
							var descriptor = await deviceHandle.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);

							// When Razer drivers are not installed, it turns out that the huge feature reports are sent on the mouse interface.
							// The tricky part here is that some HID collections are opened in exclusive mode by Windows, but we can always open HID devices without requesting any rights.
							// As it turns out that we can send or receive feature reports without any problem in that situation,
							// I don't really understand why Razer needed custom drivers in the first place. (Maybe some obscure features)
							// Also, I suspect the razer kernel driver to actually degrade performance on the system, as the mouse seems to suffer more from system hiccups than other devices.
							if (descriptor.FeatureReports.Count == 1 && descriptor.FeatureReports[0].ReportId == 0)
							{
								if (razerFeatureReportDeviceInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device control.");

								razerFeatureReportDeviceInterfaceName = deviceInterface.Id;
							}
						}
						break;
					}
				}
			}
			else if (interfaceClassGuid == DeviceInterfaceClassGuids.BluetoothGattService)
			{
				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Bluetooth.ServiceGuid.Key, out Guid serviceGuid) && serviceGuid == RazerGattServiceGuid)
				{
					razerGattServiceDeviceInterfaceName = deviceInterface.Id;
				}
			}
		}

		if (razerFeatureReportDeviceInterfaceName is null && razerControlDeviceInterfaceName is null && razerGattServiceDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("The device interface for Razer device control was not found.");
		}

		if (notificationDeviceInterfaceName is null)
		{
			throw new InvalidOperationException("The device interface for device notifications was not found.");
		}

		if (secondaryNotificationDeviceInterfaceName is not null && razerGattServiceDeviceInterfaceName is not null)
		{
			throw new InvalidOperationException("Bluetooth devices are not expected to expose a device interface for secondary notifications.");
		}

		// Instantiate an appropriate protocol transport depending on the detected configuration.
		// ⚠️ Only Razer DeathAdder V2 Pro is supported, especially for BT LE. Other devices may or may not be compatible, but it must be verified using USB/BT LE dumps.
		IRazerProtocolTransport transport;
		if (razerFeatureReportDeviceInterfaceName is not null)
		{
			transport = new HidRazerProtocolTransport(new HidDeviceStream(Device.OpenHandle(razerFeatureReportDeviceInterfaceName!, DeviceAccess.None, FileShare.None), FileAccess.ReadWrite));
		}
		else if (razerGattServiceDeviceInterfaceName is not null)
		{
			// NB: We try to open the BLE device (GATT service) in exclusive mode in order to prevent conflict with other software.
			transport = new RazerDeathAdderV2ProBluetoothProtocolTransport(Device.OpenHandle(razerGattServiceDeviceInterfaceName, DeviceAccess.ReadWrite, FileShare.None), 1_000);
		}
		else
		{
			transport = new RzControlRazerProtocolTransport(new DeviceStream(Device.OpenHandle(razerControlDeviceInterfaceName!, DeviceAccess.None, FileShare.None), FileAccess.ReadWrite));
		}

		await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);

		string? serialNumber = null;

		if (deviceInfo.DeviceCategory != RazerDeviceCategory.UsbReceiver)
		{
			serialNumber = await transport.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);
		}

		var device = await CreateDeviceAsync
		(
			transport,
			new(notificationDeviceInterfaceName, FileMode.Open, FileAccess.Read, FileShare.Read, 0, true),
			new DeviceNotificationOptions()
			{
				StreamIndex = 1,
				ReportId = 5,
				HasBluetoothHidQuirk = razerGattServiceDeviceInterfaceName is not null,
				ReportLength = notificationReportLength
			},
			secondaryNotificationDeviceInterfaceName is not null ?
				new(secondaryNotificationDeviceInterfaceName, FileMode.Open, FileAccess.Read, FileShare.Read, 0, true) :
				null,
			new DeviceNotificationOptions()
			{
				StreamIndex = 2,
				ReportId = 9,
				HasBluetoothHidQuirk = false,
				ReportLength = secondaryNotificationReportLength
			},
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
		IRazerProtocolTransport transport,
		DeviceStream notificationStream,
		DeviceNotificationOptions notificationOptions,
		DeviceStream? secondNotificationStream,
		DeviceNotificationOptions secondNotificationOptions,
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
				notificationStream,
				notificationOptions,
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
				notificationStream,
				notificationOptions,
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceInfo.MaximumDpi,
				MaximumPollingFrequency,
				SupportedPollingFrequencyDividers,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Dock => new SystemDevice.Generic
			(
				transport,
				notificationStream,
				notificationOptions,
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
				notificationStream,
				notificationOptions,
				secondNotificationStream,
				secondNotificationOptions,
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
		IRazerProtocolTransport transport,
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
				deviceInfo.LightingZoneGuid.GetValueOrDefault(),
				deviceInfo.MaximumDpi,
				MaximumPollingFrequency,
				SupportedPollingFrequencyDividers,
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Dock => new Generic
			(
				transport,
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
	private readonly IRazerProtocolTransport _transport;

	private readonly DeviceIdSource _deviceIdSource;
	private readonly ImmutableArray<DeviceId> _deviceIds;
	private readonly byte _mainDeviceIdIndex;
	private readonly RazerDeviceFlags _deviceFlags;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

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
		IRazerProtocolTransport transport,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		ImmutableArray<DeviceId> deviceIds,
		byte mainDeviceIdIndex,
		RazerDeviceFlags deviceFlags
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_deviceIds = deviceIds;
		_mainDeviceIdIndex = mainDeviceIdIndex;
		_deviceFlags = deviceFlags;
		_genericFeatures = CreateGenericFeatures();
	}

	protected virtual ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

	// The transport must only be disposed for root devices.
	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

	// Method used to dispose resources in root instances.
	// Child devices will share the parent resources, so they should not call this method.
	private void DisposeRootResources()
	{
		_transport.Dispose();
	}

	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
		=> HasSerialNumber ?
			FeatureSet.Create<IGenericDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature>(this);

	void IRazerDeviceNotificationSink.OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex) => OnDeviceArrival(notificationStreamIndex, deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId) => OnDeviceArrival(notificationStreamIndex, deviceIndex, productId);

	void IRazerDeviceNotificationSink.OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex) => OnDeviceRemoval(notificationStreamIndex, deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId) => OnDeviceRemoval(notificationStreamIndex, deviceIndex, productId);

	void IRazerDeviceNotificationSink.OnDeviceDpiChange(byte notificationStreamIndex, ushort dpiX, ushort dpiY) => OnDeviceDpiChange(notificationStreamIndex, dpiX, dpiY);
	void IRazerDeviceNotificationSink.OnDeviceExternalPowerChange(byte notificationStreamIndex, bool isConnectedToExternalPower) => OnDeviceExternalPowerChange(notificationStreamIndex, isConnectedToExternalPower);
	void IRazerDeviceNotificationSink.OnDeviceBatteryLevelChange(byte notificationStreamIndex, byte deviceIndex, byte batteryLevel) => OnDeviceBatteryLevelChange(deviceIndex, batteryLevel);

	protected virtual void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex) => throw new NotSupportedException();
	protected virtual void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId) => throw new NotSupportedException();

	protected virtual void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex) => throw new NotSupportedException();
	protected virtual void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId) => throw new NotSupportedException();

	protected virtual void OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY) => OnDeviceDpiChange(dpiX, dpiY);
	protected virtual void OnDeviceExternalPowerChange(byte deviceIndex, bool isConnectedToExternalPower) => OnDeviceExternalPowerChange(isConnectedToExternalPower);
	protected virtual void OnDeviceBatteryLevelChange(byte deviceIndex, byte batteryLevel) => OnDeviceBatteryLevelChange(batteryLevel);

	protected virtual void OnDeviceDpiChange(ushort dpiX, ushort dpiY) => throw new NotSupportedException();
	protected virtual void OnDeviceExternalPowerChange(bool isConnectedToExternalPower) => throw new NotSupportedException();
	protected virtual void OnDeviceBatteryLevelChange(byte batteryLevel) => throw new NotSupportedException();
}
