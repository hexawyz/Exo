using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features.LightingFeatures;
using Exo.Features.MonitorFeatures;

namespace Exo.Devices.Lg.Monitors;

[ProductId(VendorIdSource.Usb, 0x043E, 0x9A8A)]
public class LgMonitorDriver : HidDriver, IDeviceDriver<IMonitorDeviceFeature>
{
	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	public static async Task<LgMonitorDriver> CreateAsync(string deviceName, CancellationToken cancellationToken)
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
				Properties.System.DeviceInterface.Hid.VendorId == 0x043E,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Expected two HID device interfaces.");
		}

		// Find the top-level device by requesting devices with children.
		// The device tree should be very simple in this case, so we expect this to directly return the top level device. It would not work on more complex scenarios.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			Array.Empty<Property>(),
			Properties.System.Devices.ContainerId == containerId & Properties.System.Devices.Children.Exists(),
			cancellationToken
		).ConfigureAwait(false);

		if (devices.Length != 3)
		{
			throw new InvalidOperationException("Expected three parent devices.");
		}

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
		string? deviceInterfaceName = null;
		string topLevelDeviceName = devices[0].Id;

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDeviceName;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];
			deviceNames[i] = deviceInterface.Id;

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage))
			{
				throw new InvalidOperationException($"No HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if ((usagePage & 0xFFFE) != 0xFF00)
			{
				throw new InvalidOperationException($"Unexpected HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId))
			{
				throw new InvalidOperationException($"No HID Usage ID associated with the device interface {deviceInterface.Id}.");
			}

			if (usagePage == 0xFF00 && usageId == 0x01)
			{
				deviceInterfaceName = deviceInterface.Id;
			}
		}

		if (deviceInterfaceName is null)
		{
			throw new InvalidOperationException($"Could not find device interface with correct HID usages on the device interface {devices[0].Id}.");
		}

		byte sessionId = (byte)Random.Shared.Next(1, 256);
		var buffer = GC.AllocateArray<byte>(65, true);
		var hidStream = new HidFullDuplexStream(deviceInterfaceName);
		try
		{
			await GetDeviceInformationAsync(hidStream, sessionId, buffer, cancellationToken).ConfigureAwait(false);
			return new LgMonitorDriver
			(
				new HidFullDuplexStream(deviceInterfaceName),
				Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
				friendlyName
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private static async Task GetDeviceInformationAsync(HidFullDuplexStream hidStream, byte sessionId, byte[] buffer, CancellationToken cancellationToken)
	{
		Array.Clear(buffer);

		buffer[1] = 0x0C;
		buffer[3] = sessionId;
		buffer[4] = 0x01;
		buffer[5] = 0x80;
		buffer[6] = 0x1a;
		buffer[7] = 0x06;

		var memory = MemoryMarshal.CreateFromPinnedArray(buffer, 0, buffer.Length);

		await hidStream.WriteAsync(memory, cancellationToken).ConfigureAwait(false);

		Array.Clear(buffer);

		while (true)
		{
			int count = await hidStream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
			if (count != buffer.Length)
			{
				throw new EndOfStreamException("The connection to the device has ended abruptly.");
			}

			if (buffer[1] == 0x0c && buffer[2] == 0x00 && buffer[3] == sessionId && buffer[4] == 0x00)
			{
				if (buffer.AsSpan(10, 3).SequenceEqual("HID"u8))
				{
					break;
				}
				else
				{
					throw new InvalidDataException("The device handshake failed because invalid data was returned.");
				}
			}
		}
	}

	private readonly HidFullDuplexStream _hidFullDuplexStream;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;

	private LgMonitorDriver
	(
		HidFullDuplexStream hidFullDuplexStream,
		ImmutableArray<string> deviceNames,
		string friendlyName
	) : base(deviceNames, friendlyName, default)
	{
		_hidFullDuplexStream = hidFullDuplexStream;
		_monitorFeatures = FeatureCollection.Empty<IMonitorDeviceFeature>();
		_allFeatures = FeatureCollection.Empty<IDeviceFeature>();
	}

	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
