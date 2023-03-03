using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Logitech.HidPlusPlus;
using Exo.Devices.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using Exo.Features;

namespace Exo.Devices.Logitech;

[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
[VendorId(VendorIdSource.Usb, 0x046D)]
public class LogitechUniversalDriver : HidDriver, IDeviceDriver<IKeyboardDeviceFeature>
{
	private readonly struct ProductCategoryRange
	{
		public readonly ushort Start;
		public readonly ushort End;
		public readonly ProductCategory Category;

		public ProductCategoryRange(ushort start, ushort end, ProductCategory category)
		{
			Start = start;
			End = end;
			Category = category;
		}
	}

	// Logitech Unifying extension for Google Chrome gives a rudimentary mapping between product IDs and categories.
	// This is quite old, so it might not be perfect, but we can build on it to keep a relatively up-to-date mapping.
	// From this mapping, we can infer if the device is corded, wireless, or a receiver.
	// For HID++ 1.0, the device index to use for communicating with the device itself will be 0 for corded devices, but 255 for receivers.
	// For HID++ 2.0, it should always be 255.
	private static readonly ProductCategoryRange[] ProductIdCategoryMappings = new ProductCategoryRange[]
	{
		new(0x0000, 0x00FF, ProductCategory.VirtualUsbGameController),
		new(0x0400, 0x040F, ProductCategory.UsbScanner),
		new(0x0800, 0x08FF, ProductCategory.UsbCamera),
		new(0x0900, 0x09FF, ProductCategory.UsbCamera),
		new(0x0A00, 0x0AFF, ProductCategory.UsbAudio),
		new(0x0B00, 0x0BFF, ProductCategory.UsbHub),
		new(0x1000, 0x1FFF, ProductCategory.QuadMouse),
		new(0x2000, 0x2FFF, ProductCategory.QuadKeyboard),
		new(0x3000, 0x3FFF, ProductCategory.QuadGamingDevice),
		new(0x4000, 0x4FFF, ProductCategory.QuadFapDevice),
		new(0x5000, 0x5FFF, ProductCategory.UsbToolsTransceiver),
		new(0x8000, 0x87FF, ProductCategory.QuadMouseTransceiver),
		new(0x8800, 0x88FF, ProductCategory.QuadDesktopTransceiver),
		new(0x8900, 0x89FF, ProductCategory.UsbCamera),
		new(0x8A00, 0x8FFF, ProductCategory.QuadDesktopTransceiver),
		new(0x9000, 0x98FF, ProductCategory.QuadGamingTransceiver),
		new(0x9900, 0x99FF, ProductCategory.UsbCamera),
		new(0x9A00, 0x9FFF, ProductCategory.QuadGamingTransceiver),
		new(0xA000, 0xAFFF, ProductCategory.UsbSpecial),
		new(0xB000, 0xB0FF, ProductCategory.BluetoothMouse),
		new(0xB300, 0xB3DF, ProductCategory.BluetoothKeyboard),
		new(0xB3E0, 0xB3FF, ProductCategory.BluetoothNumpad),
		new(0xB400, 0xB4FF, ProductCategory.BluetoothRemoteControl),
		new(0xB500, 0xB5FF, ProductCategory.BluetoothReserved),
		new(0xBA00, 0xBAFF, ProductCategory.BluetoothAudio),
		new(0xC000, 0xC0FF, ProductCategory.UsbMouse),
		new(0xC100, 0xC1FF, ProductCategory.UsbRemoteControl),
		new(0xC200, 0xC2FF, ProductCategory.UsbPcGamingDevice),
		new(0xC300, 0xC3FF, ProductCategory.UsbKeyboard),
		new(0xC400, 0xC4FF, ProductCategory.UsbTrackBall),
		new(0xC500, 0xC5FF, ProductCategory.UsbReceiver),
		new(0xC600, 0xC6FF, ProductCategory.Usb3dControlDevice),
		new(0xC700, 0xC7FF, ProductCategory.UsbBluetoothReceiver),
		new(0xC800, 0xC8FF, ProductCategory.UsbOtherPointingDevice),
		new(0xCA00, 0xCCFF, ProductCategory.UsbConsoleGamingDevice),
		new(0xD000, 0xD00F, ProductCategory.UsbCamera),
		new(0xF000, 0xF00F, ProductCategory.UsbToolsTransceiver),
		new(0xF010, 0xF010, ProductCategory.UsbToolsCorded),
		new(0xF011, 0xFFFF, ProductCategory.UsbToolsTransceiver),
	};

	/// <summary>Tries to infer the logitech product category from the Product ID.</summary>
	/// <remarks>This method can't be guaranteed to be 100% exact, but should work in a lot of cases.</remarks>
	/// <param name="productId">The product ID.</param>
	/// <param name="category">The product category.</param>
	/// <returns><c>true</c> if the product category could be inferred from known data; otherwise <c>false</c>.</returns>
	public static bool TryInferProductCategory(ushort productId, out ProductCategory category)
	{
		int min = 0;
		int max = ProductIdCategoryMappings.Length - 1;

		while (min < max)
		{
			int med = (min + max) / 2;

			var item = ProductIdCategoryMappings[med];

			if (productId >= item.Start && productId <= item.End)
			{
				category = item.Category;
				return true;
			}
		}

		category = ProductCategory.Other;
		return false;
	}

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.VendorId,
		Properties.System.DeviceInterface.Hid.ProductId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	public static async Task<Driver> CreateAsync(string deviceName, CancellationToken cancellationToken)
	{
		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string? friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false);

		// Make a device query to fetch all the matching device interfaces at once.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId &
				Properties.System.DeviceInterface.Hid.VendorId == 0x046D,
			cancellationToken
		).ConfigureAwait(false);

		string[] deviceNames = new string[devices.Length];
		HidPlusPlusProtocolFlavor protocolFlavor = default;
		byte defaultDeviceIndex = 0xFF;
		string? shortInterfaceName = null;
		string? longInterfaceName = null;
		string? veryLongInterfaceName = null;
		SupportedReports discoveredReports = 0;
		SupportedReports expectedReports = 0;

		for (int i = 0; i < devices.Length; i++)
		{
			var device = devices[i];
			deviceNames[i] = device.Id;

			if (!device.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage)) continue;
			if (usagePage is not 0xFF00 and not 0xFF43) continue;
			if (!device.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId)) continue;

			var currentReport = (SupportedReports)(byte)usageId;

			switch (currentReport)
			{
			case SupportedReports.Short:
				if (shortInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ short reports.");
				shortInterfaceName = device.Id;
				break;
			case SupportedReports.Long:
				if (longInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ long reports.");
				longInterfaceName = device.Id;
				break;
			case SupportedReports.VeryLong:
				// For HID++ 1.0, this could (likely?) be a DJ interface. We don't want anything to do with that here. (At least for now)
				if (usagePage == 0xFF00)
				{
					// This is the most basic check that we can do here. We verify that the input/output report length is 64 bytes.
					// DJ reports are 15 and 32 bytes long, so the API would return 32 as the maximum report length.
					using var hid = HidDevice.FromPath(device.Id);
					var (il, ol, fl) = hid.GetReportLengths();
					if (!(il == 64 && ol == 64 && fl == 0)) continue;
				}
				if (veryLongInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ very long reports.");
				veryLongInterfaceName = device.Id;
				break;
			default:
				break;
			}

			discoveredReports |= currentReport;

			// FF00 for the old scheme (HID++ 1.0) and FF43 for the new scheme (HID++ 2.0)
			if (usagePage == 0xFF43)
			{
				var currentExpectedReports = (SupportedReports)(byte)(usageId >>> 8);

				if (expectedReports == 0)
				{
					expectedReports = currentExpectedReports;
				}
				else if (expectedReports != currentExpectedReports)
				{
					throw new InvalidOperationException("This device has inconsistent interfaces.");
				}

				if (protocolFlavor == HidPlusPlusProtocolFlavor.RegisterAccess)
				{
					throw new InvalidOperationException("This device has inconsistent interfaces.");
				}

				protocolFlavor = HidPlusPlusProtocolFlavor.FeatureAccess;
			}
			else if (usagePage == 0xFF00)
			{
				// HID++ 1.0 should always support short reports (correct?)
				expectedReports = expectedReports | currentReport | SupportedReports.Short;

				if (protocolFlavor == HidPlusPlusProtocolFlavor.FeatureAccess)
				{
					throw new InvalidOperationException("This device has inconsistent interfaces.");
				}

				protocolFlavor = HidPlusPlusProtocolFlavor.RegisterAccess;
			}
		}

		if (discoveredReports == 0)
		{
			throw new InvalidOperationException("No valid HID++ interface found.");
		}
		else if (discoveredReports != expectedReports)
		{
			throw new InvalidOperationException($"The device is missing some expected HID++ reports. Expected {expectedReports} but got {discoveredReports}.");
		}

		var hppDevice = await HidPlusPlusDevice.CreateAsync
		(
			shortInterfaceName is not null ? new HidFullDuplexStream(shortInterfaceName) : null,
			longInterfaceName is not null ? new HidFullDuplexStream(longInterfaceName) : null,
			veryLongInterfaceName is not null ? new HidFullDuplexStream(veryLongInterfaceName) : null,
			protocolFlavor,
			//defaultDeviceIndex,
			0x01, // Hardcoded value for the software ID. Hoping it will not conflict with anything.
			new TimeSpan(100 * TimeSpan.TicksPerSecond)
		);

		if (hppDevice is FeatureAccessDevice fapDevice)
		{
			var features = await fapDevice.GetFeaturesAsync(cancellationToken).ConfigureAwait(false);
			string? serialNumber = null;
			DeviceType? deviceType = null;

			if (features.TryGetValue(HidPlusPlusFeature.DeviceNameAndType, out byte featureIndex))
			{
				var deviceTypeResponse = await fapDevice.SendAsync<DeviceNameAndType.GetDeviceType.Response>
				(
					featureIndex,
					DeviceNameAndType.GetDeviceType.FunctionId,
					cancellationToken
				).ConfigureAwait(false);

				deviceType = deviceTypeResponse.DeviceType;

				var deviceNameLengthResponse = await fapDevice.SendAsync<DeviceNameAndType.GetDeviceNameLength.Response>
				(
					featureIndex,
					DeviceNameAndType.GetDeviceNameLength.FunctionId,
					cancellationToken
				).ConfigureAwait(false);

				int length = deviceNameLengthResponse.Length;
				int offset = 0;

				var buffer = new byte[length];

				while (true)
				{
					var deviceNameResponse = await fapDevice.SendAsync<DeviceNameAndType.GetDeviceName.Request, DeviceNameAndType.GetDeviceName.Response>
					(
						featureIndex,
						DeviceNameAndType.GetDeviceName.FunctionId,
						new DeviceNameAndType.GetDeviceName.Request { Offset = (byte)offset },
						cancellationToken
					).ConfigureAwait(false);

					if (deviceNameResponse.TryCopyTo(buffer.AsSpan(offset), out int count))
					{
						offset += count;

						if (offset == length)
						{
							break;
						}
						else if (count == 16)
						{
							continue;
						}
					}

					throw new InvalidOperationException("Failed to retrieve the device name.");
				}

				friendlyName = Encoding.UTF8.GetString(buffer);
			}

			if (features.TryGetValue(HidPlusPlusFeature.DeviceInformation, out featureIndex))
			{
				var deviceInfoResponse = await fapDevice.SendAsync<DeviceInformation.GetDeviceInfo.Response>
				(
					featureIndex,
					DeviceInformation.GetDeviceInfo.FunctionId,
					cancellationToken
				).ConfigureAwait(false);

				if ((deviceInfoResponse.Capabilities & DeviceCapabilities.SerialNumber) != 0)
				{
					var serialNumberResponse = await fapDevice.SendAsync<DeviceInformation.GetDeviceSerialNumber.Response>
					(
						featureIndex,
						DeviceInformation.GetDeviceSerialNumber.FunctionId,
						cancellationToken
					).ConfigureAwait(false);

					serialNumber = serialNumberResponse.SerialNumber;
				}
			}

			// TODO: The device name that we bind for configuration should be the top-level device of all the HID collections.
			// HID++ devices will expose multiple interfaces, each with their own top-level collection.
			// Typically for Mouse/Keyboard/Receiver, these would be 00: Boot Keyboard, 01: Input stuff, 02: HID++/DJ
			// We want to take the device that is just above all these interfaces. So, typically the name of a raw USB or BT device.
			var configurationKey = new DeviceConfigurationKey("logi", "TODO", deviceType is null ? "logi-universal" : deviceType.GetValueOrDefault().ToString(), serialNumber);

			return new LogitechUniversalDriver
			(
				hppDevice,
				Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
				friendlyName ?? "Logi HID++ device",
				configurationKey
			);
		}
		else
		{
			throw new NotImplementedException("TODO: RAP");
		}
	}

	private readonly HidPlusPlusDevice _device;

	protected LogitechUniversalDriver(HidPlusPlusDevice device, ImmutableArray<string> deviceNames, string friendlyName, DeviceConfigurationKey configurationKey)
		: base(deviceNames, friendlyName, configurationKey)
	{
		_device = device;
	}

	public override ValueTask DisposeAsync() => _device.DisposeAsync();

	public override IDeviceFeatureCollection<IDeviceFeature> Features => throw new NotImplementedException();

	IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features { get; }
}
