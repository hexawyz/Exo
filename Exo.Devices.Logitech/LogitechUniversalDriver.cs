using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Logitech.HidPlusPlus;
using Exo.Devices.Logitech.HidPlusPlus.Features;
using Exo.Features;

namespace Exo.Devices.Logitech;

[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
[VendorId(VendorIdSource.Usb, 0x046D)]
public class LogitechUniversalDriver : HidDriver, IDeviceDriver<IKeyboardDeviceFeature>
{
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
		// By retrieving the contaienrId, we'll be able to get all HID devices interfaces of the physical device at once.
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
		string? hidPlusPlusInterfaceName = null;
		HidPlusPlusProtocolFlavor protocolFlavor = default;
		byte defaultDeviceIndex = 0xFF;

		for (int i = 0; i < devices.Length; i++)
		{
			var device = devices[i];
			deviceNames[i] = device.Id;

			if (device.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage))
			{
				// FF00 for HID++ 1.0 and FF43 for HID++ 2.0
				if (usagePage == 0xFF43)
				{
					hidPlusPlusInterfaceName = device.Id;
					protocolFlavor = HidPlusPlusProtocolFlavor.HidPlusPlus2;
				}
				else if (usagePage == 0xFF00)
				{
					hidPlusPlusInterfaceName = device.Id;
					protocolFlavor = HidPlusPlusProtocolFlavor.HidPlusPlus1;
				}
			}
		}

		if (hidPlusPlusInterfaceName is null)
		{
			throw new InvalidOperationException("No valid HID++ device found.");
		}

		var hppDevice = await HidPlusPlusDevice.CreateAsync
		(
			new HidFullDuplexStream(hidPlusPlusInterfaceName),
			protocolFlavor,
			defaultDeviceIndex,
			0x01, // Hardcoded value for the software ID. Hoping it will not conflict with anything.
			new TimeSpan(100 * TimeSpan.TicksPerSecond)
		);

		var features = await hppDevice.GetFeaturesAsync(defaultDeviceIndex, cancellationToken).ConfigureAwait(false);
		string? serialNumber = null;
		DeviceType? deviceType = null;

		if (features.TryGetValue(HidPlusPlusFeature.DeviceNameAndType, out byte featureIndex))
		{
			var deviceTypeResponse = await hppDevice.SendAsync<DeviceNameAndType.GetDeviceType.Response>
			(
				defaultDeviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceType.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			deviceType = deviceTypeResponse.DeviceType;

			var deviceNameLengthResponse = await hppDevice.SendAsync<DeviceNameAndType.GetDeviceNameLength.Response>
			(
				defaultDeviceIndex,
				featureIndex,
				DeviceNameAndType.GetDeviceNameLength.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			int length = deviceNameLengthResponse.Length;
			int offset = 0;

			var buffer = new byte[length];

			while (true)
			{
				var deviceNameResponse = await hppDevice.SendAsync<DeviceNameAndType.GetDeviceName.Request, DeviceNameAndType.GetDeviceName.Response>
				(
					defaultDeviceIndex,
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
			var deviceInfoResponse = await hppDevice.SendAsync<DeviceInformation.GetDeviceInfo.Response>
			(
				defaultDeviceIndex,
				featureIndex,
				DeviceInformation.GetDeviceInfo.FunctionId,
				cancellationToken
			).ConfigureAwait(false);

			if ((deviceInfoResponse.Capabilities & DeviceCapabilities.SerialNumber) != 0)
			{
				var serialNumberResponse = await hppDevice.SendAsync<DeviceInformation.GetDeviceSerialNumber.Response>
				(
					defaultDeviceIndex,
					featureIndex,
					DeviceInformation.GetDeviceSerialNumber.FunctionId,
					cancellationToken
				).ConfigureAwait(false);

				serialNumber = serialNumberResponse.SerialNumber;
			}
		}

		var configurationKey = new DeviceConfigurationKey("logi", hidPlusPlusInterfaceName, deviceType is null ? "logi-universal" : deviceType.GetValueOrDefault().ToString(), serialNumber);

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

	public void Dispose()
	{
		_device.Dispose();
	}

	public override IDeviceFeatureCollection<IDeviceFeature> Features => throw new NotImplementedException();

	IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features { get; }
}
