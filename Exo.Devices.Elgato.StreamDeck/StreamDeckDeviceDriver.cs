using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Features;

namespace Exo.Devices.Elgato.StreamDeck;

// TODO: This driver should be relatively simple to write on the basic principle, but care should be taken on how to expose the device features in a way that is generic enough, but not overly generic.
// Example of features we may want to support in the service:
//  - Using the device with its basic feature set, which is setting an image on a button and catching button presses/releases.
//  - Providing a feature set very similar to the official stream deck software with buttons associating icons and features.
//  - Using buttons as lighting zones
//  - Using buttons as an hybrid of lighting zones and function buttons (e.g. transparent icons with live-colored background)
//  - Entirely customizing button actions
//  - Triggering internal configuration changes on button presses
//  - Using the device or part of the device as an external screen
//  - Entirely separating the display & input sides of the stream deck (interesting with lighting and screen modes)
// As with the lighting stuff, these various usage modes should be implemented in a way that don't conflict with each other, and in a way that is comprehensible by the user.
// A big part of the feature set would probably be handled by the event system that has yet to be implemented, but the driver need to be able to communicate events appropriately.
// Currently, there isn't yet an interface designed to expose generic button presses in the way the stream deck would do, and as such, no service to process this.
// Devices similar to the stream deck, such as the loupe deck, would provide additional controls such as various dials and buttons. (Or buttons that can only support reduced lighting)
// The Stream Deck core feature could be exposed as its own kind of feature, e.g. "custom button array", and other buttons could use a more generic mechanism. Wouldn't solve image stuff though.
// Or all buttons could be exposed as a generic feature… Depends on what we want to do.
// Generally the idea would be to expose stuff as close to what the hardware support, but that would require very elgato-specific stuff and restrict a bit the fun that can be implemented.
// The first part may be fine, but the second would be a bit more annoying.

//[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x0060)] // Stream Deck (Untested)
//[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x0063)] // Stream Deck Mini (Untested)
[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x006C)]
public sealed class StreamDeckDeviceDriver : Driver, ISystemDeviceDriver, IDeviceIdFeature, ISerialNumberDeviceFeature
{
	private const ushort ElgatoVendorId = 0x0FD9;

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.InterfaceClassGuid,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	private static readonly Property[] RequestedDeviceProperties = new Property[]
	{
		Properties.System.Devices.BusTypeGuid,
	};

	public static async Task<StreamDeckDeviceDriver> CreateAsync
	(
		string deviceName,
		ushort productId,
		ushort version,
		CancellationToken cancellationToken
	)
	{
		// It seems that the two protocol versions are quite similar, and a lot of information on them is available on the internet.
		// However, I don't have a V1 device to try stuff on, so the initial implementation will only work on V2.
		// See here for some references:
		//  - V1+: https://gist.github.com/cliffrowley/d18a9c4569537b195f2b1eb6c68469e0
		//  - V2: https://den.dev/blog/reverse-engineering-stream-deck/
		if (version != 0x0200)
		{
			throw new InvalidOperationException("Only version 2 of the Stream Deck protocol is supported.");
		}

		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the device interfaces. We already know the device interface name, so this is just for registering the driver.
		var deviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Unexpected number of device interfaces associated with the device.");
		}

		// Make a device query to fetch all the devices. This is also for the sake of registering the device itself.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			RequestedDeviceProperties,
			Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		if (devices.Length != 2)
		{
			throw new InvalidOperationException("Unexpected number of device nodes associated with the device.");
		}

		// We'll hardcode the order as such: HID DI, USB DI, HID D, USB D
		string[] deviceNames = new string[deviceInterfaces.Length + devices.Length];

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid guid) && guid == DeviceInterfaceClassGuids.Hid)
			{
				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
					usagePage == (ushort)HidUsagePage.Consumer && usageId == (ushort)HidConsumerUsage.ConsumerControl)
				{
					deviceNames[0] = deviceInterface.Id;
				}
				else
				{
					throw new InvalidOperationException("Unexpected number of device nodes associated with the device.");
				}
			}
			else
			{
				deviceNames[1] = deviceInterface.Id;
			}
		}

		for (int i = 0; i < devices.Length; i++)
		{
			var device = devices[i];

			if (device.Properties.TryGetValue(Properties.System.Devices.BusTypeGuid.Key, out Guid guid) && guid == DeviceBusTypesGuids.Hid)
			{
				deviceNames[2] = device.Id;
			}
			else
			{
				deviceNames[3] = device.Id;
			}
		}

		string serialNumber;

		var stream = new HidFullDuplexStream(deviceName);
		var buffer = ArrayPool<byte>.Shared.Rent(32);
		try
		{
			buffer[0] = 6;
			stream.ReceiveFeatureReport(buffer.AsSpan(0, 32));
			serialNumber = Encoding.ASCII.GetString(buffer.AsSpan(2, buffer[1]));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}

		return new StreamDeckDeviceDriver
		(
			stream,
			friendlyName,
			productId,
			version,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			new("StreamDeck", deviceNames[^1], $"{ElgatoVendorId:X4}:{productId:X4}", serialNumber)
		);
	}

	private readonly HidFullDuplexStream _stream;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly ImmutableArray<string> _deviceNames;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	private StreamDeckDeviceDriver
	(
		HidFullDuplexStream stream,
		string friendlyName,
		ushort productId,
		ushort versionNumber,
		ImmutableArray<string> deviceNames,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_stream = stream;
		_productId = productId;
		_versionNumber = versionNumber;
		_deviceNames = deviceNames;

		_allFeatures = FeatureCollection.Create<IDeviceFeature, StreamDeckDeviceDriver, IDeviceIdFeature, ISerialNumberDeviceFeature>(this);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
	public ImmutableArray<string> DeviceNames => _deviceNames;

	public override async ValueTask DisposeAsync()
	{
		await _stream.DisposeAsync().ConfigureAwait(false);
	}


	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(ElgatoVendorId, _productId, _versionNumber);

	string ISerialNumberDeviceFeature.SerialNumber => ConfigurationKey.UniqueId!;
}
