using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol;
using DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.Registers;
using Exo.Features;

namespace Exo.Devices.Logitech;

// This driver is a catch-all for logitech devices. On first approximation, they should all implement the proprietary HID++ protocol.
[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
[VendorId(VendorIdSource.Usb, 0x046D)]
public class LogitechUniversalDriver : HidDriver, IDeviceDriver<IKeyboardDeviceFeature>
{
	// Hardcoded value for the software ID. Hoping it will not conflict with anything still in use today.
	private const int SoftwareId = 3;

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.ProductId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	private static readonly Property[] RequestedDeviceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.Parent,
	};

	public static async Task<Driver> CreateAsync(string deviceName, CancellationToken cancellationToken)
	{
		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the matching HID device interfaces at once.
		var deviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId &
				Properties.System.DeviceInterface.Hid.VendorId == 0x046D,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length == 0)
		{
			throw new InvalidOperationException("No device interfaces compatible with logitech HID++ found.");
		}

		// Also fetch all the devices with the same container ID, so that we can find the top-level device.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			RequestedDeviceProperties,
			Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		if (devices.Length == 0)
		{
			throw new InvalidOperationException();
		}

		var parentDevices = devices.ToDictionary(d => (string)d.Properties[Properties.System.Devices.DeviceInstanceId.Key]!, d => (string)d.Properties[Properties.System.Devices.Parent.Key]!);

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
		HidPlusPlusProtocolFlavor protocolFlavor = default;
		string? shortInterfaceName = null;
		string? longInterfaceName = null;
		string? veryLongInterfaceName = null;
		SupportedReports discoveredReports = 0;
		SupportedReports expectedReports = 0;
		ushort productId = 0;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];
			deviceNames[i] = deviceInterface.Id;

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.ProductId.Key, out ushort pid))
			{
				throw new InvalidOperationException($"No HID product ID associated with the device interface {deviceInterface.Id}.");
			}

			if (productId == 0)
			{
				productId = pid;
			}
			else if (pid != productId)
			{
				throw new InvalidOperationException($"Inconsistent product ID for the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out string? deviceInstanceId))
			{
				throw new InvalidOperationException($"No device instance ID found for device interface {deviceInterface.Id}.");
			}

			// We must go from Device Interface to Device (Top Level Collection) to Device (USB/BT Interface) to Device (Parent)
			// Like most code here, we don't expect this to fail in normal conditions, so throwing an exception here is acceptable.
			var topLevelDeviceName = parentDevices[parentDevices[deviceInstanceId]];

			// We also verify that all device interfaces point towards the same top level parent. Otherwise, it would indicate that the logic should be reworked.
			// PS: I don't know, if there is a simple way to detect if a device node is a multi interface device node or not, hence the naïve lookup above where we assume a static structure.
			if (deviceNames[^1] is null)
			{
				deviceNames[^1] = topLevelDeviceName;
			}
			else if (deviceNames[^1] != topLevelDeviceName)
			{
				throw new InvalidOperationException("Top level devices don't match.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage)) continue;
			if (usagePage is not 0xFF00 and not 0xFF43) continue;
			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId)) continue;

			var currentReport = (SupportedReports)(byte)usageId;

			switch (currentReport)
			{
			case SupportedReports.Short:
				if (shortInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ short reports.");
				shortInterfaceName = deviceInterface.Id;
				break;
			case SupportedReports.Long:
				if (longInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ long reports.");
				longInterfaceName = deviceInterface.Id;
				break;
			case SupportedReports.VeryLong:
				// For HID++ 1.0, this could (likely?) be a DJ interface. We don't want anything to do with that here. (At least for now)
				if (usagePage == 0xFF00)
				{
					// This is the most basic check that we can do here. We verify that the input/output report length is 64 bytes.
					// DJ reports are 15 and 32 bytes long, so the API would return 32 as the maximum report length.
					using var hid = HidDevice.FromPath(deviceInterface.Id);
					var (il, ol, fl) = hid.GetReportLengths();
					if (!(il == 64 && ol == 64 && fl == 0)) continue;
				}
				if (veryLongInterfaceName is not null) throw new InvalidOperationException("Found two device interfaces that could map to HID++ very long reports.");
				veryLongInterfaceName = deviceInterface.Id;
				break;
			default:
				break;
			}

			discoveredReports |= currentReport;

			// FF43 for the new scheme (HID++ 2.0) and FF00 for the old scheme (any version ?)
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

		var connectionType = DeviceConnectionType.Unknown;

		if (HidPlusPlusDevice.TryInferProductCategory(productId, out var category))
		{
			connectionType = category.InferConnectionType();
		}

		var hppDevice = await HidPlusPlusDevice.CreateAsync
		(
			shortInterfaceName is not null ? new HidFullDuplexStream(shortInterfaceName) : null,
			longInterfaceName is not null ? new HidFullDuplexStream(longInterfaceName) : null,
			veryLongInterfaceName is not null ? new HidFullDuplexStream(veryLongInterfaceName) : null,
			protocolFlavor,
			productId,
			SoftwareId,
			friendlyName,
			new TimeSpan(100 * TimeSpan.TicksPerSecond)
		);

		// HID++ devices will expose multiple interfaces, each with their own top-level collection.
		// Typically for Mouse/Keyboard/Receiver, these would be 00: Boot Keyboard, 01: Input stuff, 02: HID++/DJ
		// We want to take the device that is just above all these interfaces. So, typically the name of a raw USB or BT device.
		//var configurationKey = new DeviceConfigurationKey("logi", deviceNames[^1], deviceType is null ? "logi-universal" : deviceType.GetValueOrDefault().ToString(), serialNumber);
		var configurationKey = new DeviceConfigurationKey("logi", deviceNames[^1], "logi-universal", hppDevice.SerialNumber);

		return new LogitechUniversalDriver
		(
			hppDevice,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName ?? "Logi HID++ device",
			configurationKey
		);
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

	//private class LogitechRegisterAccessProtocolUniversalDriver : LogitechUniversalDriver
	//{
	//}

	//private class LogitechFeatureAccessProtocolUniversalDriver : LogitechUniversalDriver
	//{
	//}
}
