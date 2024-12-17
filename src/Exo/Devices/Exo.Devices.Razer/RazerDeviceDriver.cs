using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
	IRazerPeriodicEventHandler,
	IDeviceSerialNumberFeature,
	IDeviceIdFeature,
	IDeviceIdsFeature
{
	private const ushort RazerVendorId = 0x1532;

	private const ushort MaximumPollingFrequency = 1000;

	private const int ManualPollingInterval = 60_000;

	// These values express the frequency dividers in the Log2 form.
	// e.g. [0, 1, 3] correspond to dividers [1, 2, 8]
	private static readonly ImmutableArray<byte> SupportedPollingFrequencyDividerPowers = [0, 1, 3];

	// It does not seem we can retrieve enough metadata from the devices themselves, so we need to have some manually entered data here.
	private readonly struct DeviceInformation
	{
		public DeviceInformation
		(
			RazerDeviceCategory deviceCategory,
			RazerLedId mainLedId,
			RazerDeviceFlags flags,
			ushort wiredDeviceProductId,
			ushort dongleDeviceProductId,
			ushort bluetoothDeviceProductId,
			ushort bluetoothLowEnergyDeviceProductId,
			Guid? lightingZoneGuid,
			ushort maximumDpi,
			string friendlyName,
			ImmutableArray<ushort> defaultDpiPresets
		)
		{
			DeviceCategory = deviceCategory;
			MainLedId = mainLedId;
			Flags = flags;
			MaximumDpi = maximumDpi;
			WiredDeviceProductId = wiredDeviceProductId;
			DongleDeviceProductId = dongleDeviceProductId;
			BluetoothDeviceProductId = bluetoothDeviceProductId;
			BluetoothLowEnergyDeviceProductId = bluetoothLowEnergyDeviceProductId;
			LightingZoneGuid = lightingZoneGuid;
			FriendlyName = friendlyName;
			DefaultDpiPresets = defaultDpiPresets;
		}

		public RazerDeviceCategory DeviceCategory { get; }

		public RazerLedId MainLedId { get; }

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

		// DPI presets for when the device does not support reading them 😓
		// Unless I've made a mistake in the read command, this is necessary to avoid weird behavior.
		public ImmutableArray<ushort> DefaultDpiPresets { get; }

		public bool HasWiredDeviceProductId => (ushort)(WiredDeviceProductId + 1) != 0;
		public bool HasDongleProductId => (ushort)(DongleDeviceProductId + 1) != 0;
		public bool HasBluetoothProductId => (ushort)(BluetoothDeviceProductId + 1) != 0;
		public bool HasBluetoothLowEnergyProductId => (ushort)(BluetoothLowEnergyDeviceProductId + 1) != 0;

		public bool HasBattery => (Flags & RazerDeviceFlags.HasBattery) != 0;
		public bool HasPowerNotifications => (Flags & RazerDeviceFlags.HasPowerNotifications) != 0;

		public bool HasLighting => (Flags & RazerDeviceFlags.HasLighting) != 0;
		public bool HasLightingV2 => (Flags & RazerDeviceFlags.HasLightingV2) != 0;
		public bool HasReactiveLighting => (Flags & RazerDeviceFlags.HasReactiveLighting) != 0;
		public bool UseNonUnifiedLightingAsUnified => (Flags & RazerDeviceFlags.UseNonUnifiedLightingAsUnified) != 0;

		public bool IsReceiver => DeviceCategory is RazerDeviceCategory.UsbReceiver or RazerDeviceCategory.DockReceiver;

		public ushort GetMainProductId()
			=> HasDongleProductId && IsReceiver ?
				DongleDeviceProductId :
				!HasWiredDeviceProductId ?
					!HasBluetoothLowEnergyProductId ?
						!HasBluetoothProductId ?
							(ushort)0 :
							BluetoothDeviceProductId :
						BluetoothLowEnergyDeviceProductId :
					WiredDeviceProductId;

		public ImmutableArray<DeviceId> GetDeviceIds(ushort versionNumber)
		{
			if (IsReceiver) return [new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DongleDeviceProductId, versionNumber)];

			int count = 0;

			if (HasWiredDeviceProductId) count++;
			if (HasDongleProductId) count++;
			if (HasBluetoothProductId) count++;
			if (HasBluetoothLowEnergyProductId) count++;

			if (count == 0) return [];

			var productIds = new DeviceId[count];
			int index = 0;
			if (HasWiredDeviceProductId) productIds[index++] = new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, WiredDeviceProductId, versionNumber);
			if (HasDongleProductId) productIds[index++] = new DeviceId(DeviceIdSource.Usb, VendorIdSource.Usb, RazerVendorId, DongleDeviceProductId, versionNumber);
			if (HasBluetoothProductId) productIds[index++] = new DeviceId(DeviceIdSource.Bluetooth, VendorIdSource.Usb, RazerVendorId, BluetoothDeviceProductId, versionNumber);
			if (HasBluetoothLowEnergyProductId) productIds[index++] = new DeviceId(DeviceIdSource.BluetoothLowEnergy, VendorIdSource.Usb, RazerVendorId, BluetoothLowEnergyDeviceProductId, versionNumber);

			return Unsafe.As<DeviceId[], ImmutableArray<DeviceId>>(ref productIds);
		}

		public DeviceConnectionType GetConnectionType(ushort productId)
		{
			if (HasWiredDeviceProductId && productId == WiredDeviceProductId) return DeviceConnectionType.Wired;
			if (HasDongleProductId && productId == DongleDeviceProductId) return IsReceiver ? DeviceConnectionType.Wired : DeviceConnectionType.Wireless;
			if (HasBluetoothProductId && productId == BluetoothDeviceProductId) return DeviceConnectionType.Bluetooth;
			if (HasBluetoothLowEnergyProductId && productId == BluetoothLowEnergyDeviceProductId) return DeviceConnectionType.BluetoothLowEnergy;
			return DeviceConnectionType.Wired;
		}

		public DotsPerInch[] GetDefaultDpiPresets()
			=> !DefaultDpiPresets.IsDefaultOrEmpty ?
				Array.ConvertAll(ImmutableCollectionsMarshal.AsArray(DefaultDpiPresets)!, dpi => new DotsPerInch(dpi, dpi)) :
				[];

	}

	// This is a purely internal enum to provide metadata about the devices.
	private enum RazerDeviceCategory : byte
	{
		Unknown = 0,
		Keyboard = 1,
		Mouse = 2,
		UsbReceiver = 3,
		Dock = 4,
		DockReceiver = 5,
	}

	// TODO: The data structure should probably be migrated into an in-memory database similarly to what is done for monitors.
	[Flags]
	private enum RazerDeviceFlags : ushort
	{
		None = 0x00,

		HasBattery = 0x01,
		HasPowerNotifications = 0x02,
		HasWirelessMaximumBrightness = 0x04,

		HasLighting = 0x08,
		HasLightingV2 = 0x10,
		HasReactiveLighting = 0x20,
		UseNonUnifiedLightingAsUnified = 0x40,

		HasDpi = 0x80,
		HasDpiPresets = 0x100,
		HasDpiPresetsRead = 0x200,
		HasDpiPresetsV2 = 0x400,

		// Added for Mamba Chroma but maybe not necessary ?
		MustSetDeviceMode3 = 0x800,
		MustSetSensorState5 = 0x1000,
	}

	private static readonly Guid RazerControlDeviceInterfaceClassGuid = new(0xe3be005d, 0xd130, 0x4910, 0x88, 0xff, 0x09, 0xae, 0x02, 0xf6, 0x80, 0xe9);

	// This is the GUID of the BLE GATT service used by Razer DeathAdder V2 Pro and possibly other devices.
	// The BLE protocol is different from the USB HID protocol and does not require a kernel driver. Instead, it uses a custom BLE service exposed by the device.
	private static readonly Guid RazerGattServiceGuid = new(0x52401523, 0xF97C, 0x7F90, 0x0E, 0x7F, 0x6C, 0x6F, 0x4E, 0x36, 0xDB, 0x1C);

	private static readonly Guid DockLightingZoneGuid = new(0x5E410069, 0x0F34, 0x4DD8, 0x80, 0xDB, 0x5B, 0x11, 0xFB, 0xD4, 0x13, 0xD6);
	private static readonly Guid DeathAdderV2ProLightingZoneGuid = new(0x4D2EE313, 0xEA46, 0x4857, 0x89, 0x8C, 0x5B, 0xF9, 0x44, 0x09, 0x0A, 0x9A);
	private static readonly Guid MambaChromaLightingZoneGuid = new(0x16D0353A, 0xBBB9, 0x4993, 0x92, 0x3E, 0x1D, 0x09, 0x09, 0xF8, 0x96, 0xDD);
	private static readonly Guid MambaChromaDockLightingZoneGuid = new(0x810A8E83, 0xA7A2, 0x4BAE, 0x8C, 0xC8, 0xFE, 0x87, 0xDD, 0x3C, 0x8F, 0xB7);

	// Stores the device informations in a linear table that allow deduplicating information and accessing it by reference.
	// The indices into this table will be built into the dictionary.
	// NB: Accessing by reference could also be done with the unsafe dictionary APIs, but not deduplicating the data.
	// TODO: Make a source generator for this and store everything in a data block using ReadOnlySpan<byte>.
	private static readonly DeviceInformation[] DeviceInformationTable =
	[
		new
		(
			RazerDeviceCategory.Mouse,
			RazerLedId.Backlight,
			RazerDeviceFlags.HasBattery |
				RazerDeviceFlags.HasWirelessMaximumBrightness |
				RazerDeviceFlags.HasLighting |
				RazerDeviceFlags.HasReactiveLighting |
				//RazerDeviceFlags.UseNonUnifiedLightingAsUnified |
				RazerDeviceFlags.HasDpi/* |
				RazerDeviceFlags.MustSetDeviceMode3 |
				RazerDeviceFlags.MustSetSensorState5*/,
			0x0044,
			0x0045,
			0xFFFF,
			0xFFFF,
			MambaChromaLightingZoneGuid,
			16_000,
			"Razer Mamba Chroma",
			[800, 1_800, 4_500, 9_000, 16_000]
		),
		new
		(
			RazerDeviceCategory.DockReceiver,
			RazerLedId.Dongle,
			RazerDeviceFlags.HasLighting |
				RazerDeviceFlags.UseNonUnifiedLightingAsUnified,
			0x0044,
			0x0045,
			0xFFFF,
			0xFFFF,
			MambaChromaDockLightingZoneGuid,
			16_000,
			"Razer Mamba Chroma Dock",
			[]
		),
		new
		(
			RazerDeviceCategory.Mouse,
			RazerLedId.Logo,
			RazerDeviceFlags.HasBattery |
				RazerDeviceFlags.HasPowerNotifications |
				RazerDeviceFlags.HasLighting |
				RazerDeviceFlags.HasLightingV2 |
				RazerDeviceFlags.HasReactiveLighting |
				RazerDeviceFlags.HasDpi |
				RazerDeviceFlags.HasDpiPresets |
				RazerDeviceFlags.HasDpiPresetsRead |
				RazerDeviceFlags.HasDpiPresetsV2,
			0x007C,
			0x007D,
			0xFFFF,
			0x008E,
			DeathAdderV2ProLightingZoneGuid,
			20_000,
			"Razer DeathAdder V2 Pro",
			[]
		),
		new
		(
			RazerDeviceCategory.UsbReceiver,
			RazerLedId.None,
			0,
			0x007C,
			0x007D,
			0xFFFF,
			0x008E,
			null,
			0,
			"Razer DeathAdder V2 Pro HyperSpeed Dongle",
			[]
		),
		new
		(
			RazerDeviceCategory.Dock,
			RazerLedId.Backlight,
			RazerDeviceFlags.HasLighting | RazerDeviceFlags.HasLightingV2,
			0x007E,
			0xFFFF,
			0xFFFF,
			0xFFFF,
			DockLightingZoneGuid,
			0,
			"Razer Mouse Dock",
			[]
		),
		new
		(
			RazerDeviceCategory.Mouse,
			RazerLedId.Logo,
			RazerDeviceFlags.HasBattery |
				RazerDeviceFlags.HasPowerNotifications |
				RazerDeviceFlags.HasDpi |
				RazerDeviceFlags.HasDpiPresets |
				RazerDeviceFlags.HasDpiPresetsRead |
				RazerDeviceFlags.HasDpiPresetsV2,
			0x00B6,
			0x00B7,
			0xFFFF,
			0xFFFF,
			null,
			30_000,
			"Razer DeathAdder V3 Pro",
			[]
		),
		new
		(
			RazerDeviceCategory.UsbReceiver,
			RazerLedId.None,
			0,
			0x00B6,
			0x00B7,
			0xFFFF,
			0xFFFF,
			null,
			0,
			"Razer DeathAdder V3 Pro HyperSpeed Dongle",
			[]
		)
	];

	private static readonly Dictionary<ushort, ushort> DeviceInformationIndices = new()
	{
		{ 0x0044, 0 },
		{ 0x0045, 1 },
		{ 0x007C, 2 },
		{ 0x007D, 3 },
		{ 0x007E, 4 },
		{ 0x008E, 2 },
		{ 0x00B6, 5 },
		{ 0x0087, 6 },
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
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x0044)] // Mamba Chroma Mouse Wired
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x0045)] // Mamba Chroma Mouse Wireless
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007C)] // DeathAdder V2 Pro Mouse Wired
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007D)] // DeathAdder V2 Pro Mouse via Dongle
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x007E)] // DeathAdder V2 Pro Dock
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x008E)] // DeathAdder V2 Pro Mouse BLE
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x00B6)] // DeathAdder V3 Pro Mouse Wired
	[ProductId(VendorIdSource.Usb, RazerVendorId, 0x00B7)] // DeathAdder V3 Pro Mouse via Dongle
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

		// TODO: Find if there is a proper way to handshake or remove this. Serial number can be used to serve as the handshake otherwise.
		// NB: Maybe the firmware version request (00 03) can be used (to be tested), as this is the only request made to the dongle by the FW updater and it works.
		//await transport.HandshakeAsync(cancellationToken).ConfigureAwait(false);

		string? serialNumber = null;

		if (deviceInfo.DeviceCategory is RazerDeviceCategory.UsbReceiver or RazerDeviceCategory.DockReceiver)
		{
			// NB: USB receivers are likely to return no valid value here, which we will consider the null value (FF repeated 20 times)
			// As long as the call succeed, it can serve as a handshake.
			serialNumber = await transport.GetDockSerialNumberAsync(cancellationToken).ConfigureAwait(false);
		}
		else
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
			deviceInfo.GetConnectionType(productId),
			version,
			deviceInfo,
			friendlyName,
			topLevelDeviceName,
			serialNumber,
			cancellationToken
		).ConfigureAwait(false);

		// Get rid of the nested driver registry if we don't need it.
		if (device is not (SystemDevice.UsbReceiver or SystemDevice.DockReceiver))
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
		DeviceConnectionType connectionType,
		ushort versionNumber,
		DeviceInformation deviceInfo,
		string friendlyName,
		string topLevelDeviceName,
		string? serialNumber,
		CancellationToken cancellationToken
	)
	{
		ImmutableArray<RazerLedId> ledIds = [];
		if (deviceInfo.HasLighting && deviceInfo.HasLightingV2)
		{
			ledIds = await transport.GetLightingZoneIdsAsync(cancellationToken).ConfigureAwait(false);
		}

		// Determine the main device ID using priority rules.
		ushort mainProductId = deviceInfo.GetMainProductId();
		var deviceIds = deviceInfo.GetDeviceIds(versionNumber);
		byte mainDeviceIdIndex = GetDeviceIdIndex(deviceIds, mainProductId);

		var configurationKey = new DeviceConfigurationKey("RazerDevice", topLevelDeviceName, $"{RazerVendorId:X4}:{mainProductId:X4}", serialNumber);

		var periodicEventGenerator = deviceInfo.HasBattery && !deviceInfo.HasPowerNotifications ? new RazerProtocolPeriodicEventGenerator(ManualPollingInterval) : null;

		RazerDeviceDriver driver = deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new SystemDevice.Keyboard
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				notificationOptions,
				in deviceInfo,
				ledIds,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				connectionType,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Mouse => new SystemDevice.Mouse
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				notificationOptions,
				in deviceInfo,
				ledIds,
				deviceInfo.MaximumDpi,
				MaximumPollingFrequency,
				SupportedPollingFrequencyDividerPowers,
				deviceInfo.GetDefaultDpiPresets(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				connectionType,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Dock => new SystemDevice.Generic
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				notificationOptions,
				DeviceCategory.MouseDock,
				in deviceInfo,
				ledIds,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				deviceInfo.Flags,
				connectionType,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.UsbReceiver => new SystemDevice.UsbReceiver
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				notificationOptions,
				secondNotificationStream,
				secondNotificationOptions,
				driverRegistry.GetOrCreateValue(),
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				connectionType,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.DockReceiver => new SystemDevice.DockReceiver
			(
				transport,
				periodicEventGenerator,
				notificationStream,
				notificationOptions,
				driverRegistry.GetOrCreateValue(),
				in deviceInfo,
				ledIds,
				deviceInfo.FriendlyName ?? friendlyName,
				configurationKey,
				connectionType,
				deviceIds,
				mainDeviceIdIndex,
				deviceInfo.Flags
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
		RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
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
		ImmutableArray<RazerLedId> ledIds = [];
		if (deviceInfo.HasLighting && deviceInfo.HasLightingV2)
		{
			ledIds = await transport.GetLightingZoneIdsAsync(cancellationToken).ConfigureAwait(false);
		}

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

		if (periodicEventGenerator is null && deviceInfo.HasBattery && !deviceInfo.HasPowerNotifications)
		{
			periodicEventGenerator = new RazerProtocolPeriodicEventGenerator(ManualPollingInterval);
		}

		RazerDeviceDriver driver = deviceInfo.DeviceCategory switch
		{
			RazerDeviceCategory.Keyboard => new Keyboard
			(
				transport,
				periodicEventGenerator,
				in deviceInfo,
				ledIds,
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				DeviceConnectionType.Wireless,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Mouse => new Mouse
			(
				transport,
				periodicEventGenerator,
				in deviceInfo,
				ledIds,
				deviceInfo.MaximumDpi,
				MaximumPollingFrequency,
				SupportedPollingFrequencyDividerPowers,
				deviceInfo.GetDefaultDpiPresets(),
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				DeviceConnectionType.Wireless,
				deviceIds,
				mainDeviceIdIndex
			),
			RazerDeviceCategory.Dock => new Generic
			(
				transport,
				periodicEventGenerator,
				DeviceCategory.MouseDock,
				in deviceInfo,
				ledIds,
				friendlyName,
				configurationKey,
				deviceInfo.Flags,
				DeviceConnectionType.Wireless,
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

	// The periodic event generator is used to manage periodic events on the transport.
	// It *could* be merged with the transport for practical reasons but the two features are not related enough.
	private readonly RazerProtocolPeriodicEventGenerator? _periodicEventGenerator;

	private readonly DeviceConnectionType _connectionType;
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
		RazerProtocolPeriodicEventGenerator? periodicEventGenerator,
		string friendlyName,
		DeviceConfigurationKey configurationKey,
		DeviceConnectionType connectionType,
		ImmutableArray<DeviceId> deviceIds,
		byte mainDeviceIdIndex,
		RazerDeviceFlags deviceFlags
	) : base(friendlyName, configurationKey)
	{
		_transport = transport;
		_periodicEventGenerator = periodicEventGenerator;
		_connectionType = connectionType;
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
		// Disposing the periodic event generator first should reduce the risk of errors related to accessing a disposed transport.
		_periodicEventGenerator?.Dispose();
		_transport.Dispose();
	}

	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
		=> HasSerialNumber ?
			FeatureSet.Create<IGenericDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, RazerDeviceDriver, IDeviceIdFeature, IDeviceIdsFeature>(this);

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

	void IRazerDeviceNotificationSink.OnDeviceAvailabilityChange(byte notificationStreamIndex) => OnDeviceAvailabilityChange(notificationStreamIndex);

	void IRazerDeviceNotificationSink.OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex) => OnDeviceArrival(notificationStreamIndex, deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId) => OnDeviceArrival(notificationStreamIndex, deviceIndex, productId);

	void IRazerDeviceNotificationSink.OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex) => OnDeviceRemoval(notificationStreamIndex, deviceIndex);
	void IRazerDeviceNotificationSink.OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId) => OnDeviceRemoval(notificationStreamIndex, deviceIndex, productId);

	void IRazerDeviceNotificationSink.OnDeviceDpiChange(byte notificationStreamIndex, ushort dpiX, ushort dpiY) => OnDeviceDpiChange(notificationStreamIndex, dpiX, dpiY);
	void IRazerDeviceNotificationSink.OnDeviceExternalPowerChange(byte notificationStreamIndex, bool isConnectedToExternalPower) => OnDeviceExternalPowerChange(notificationStreamIndex, isConnectedToExternalPower);
	void IRazerDeviceNotificationSink.OnDeviceBatteryLevelChange(byte notificationStreamIndex, byte deviceIndex, byte batteryLevel) => OnDeviceBatteryLevelChange(deviceIndex, batteryLevel);

	protected virtual void OnDeviceAvailabilityChange(byte notificationStreamIndex) => throw new NotSupportedException();

	protected virtual void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex) { }
	protected virtual void OnDeviceArrival(byte notificationStreamIndex, byte deviceIndex, ushort productId) => throw new NotSupportedException();

	protected virtual void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex) { }
	protected virtual void OnDeviceRemoval(byte notificationStreamIndex, byte deviceIndex, ushort productId) => throw new NotSupportedException();

	protected virtual void OnDeviceDpiChange(byte deviceIndex, ushort dpiX, ushort dpiY) => OnDeviceDpiChange(dpiX, dpiY);
	protected virtual void OnDeviceExternalPowerChange(byte deviceIndex, bool isConnectedToExternalPower) => OnDeviceExternalPowerChange(isConnectedToExternalPower);
	protected virtual void OnDeviceBatteryLevelChange(byte deviceIndex, byte batteryLevel) => OnDeviceBatteryLevelChange(batteryLevel);

	protected virtual void OnDeviceDpiChange(ushort dpiX, ushort dpiY) => throw new NotSupportedException();
	protected virtual void OnDeviceExternalPowerChange(bool isConnectedToExternalPower) => throw new NotSupportedException();
	protected virtual void OnDeviceBatteryLevelChange(byte batteryLevel) => throw new NotSupportedException();
}
