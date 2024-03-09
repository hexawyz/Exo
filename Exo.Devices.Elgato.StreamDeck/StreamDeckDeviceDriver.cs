using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;

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
public sealed class StreamDeckDeviceDriver : Driver, IDeviceDriver<IGenericDeviceFeature>, IDeviceIdFeature, IDeviceSerialNumberFeature
{
	private const ushort ElgatoVendorId = 0x0FD9;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	//[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x0060)] // Stream Deck (Untested)
	//[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x0063)] // Stream Deck Mini (Untested)
	[ProductId(VendorIdSource.Usb, ElgatoVendorId, 0x006C)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		// It seems that the two protocol versions are quite similar, and a lot of information on them is available on the internet.
		// However, I don't have a V1 device to try stuff on, so the initial implementation will only work on V2.
		// See here for some references:
		//  - V1+: https://gist.github.com/cliffrowley/d18a9c4569537b195f2b1eb6c68469e0
		//  - V2: https://den.dev/blog/reverse-engineering-stream-deck/

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Unexpected number of device interfaces associated with the device.");
		}

		if (devices.Length != 2)
		{
			throw new InvalidOperationException("Unexpected number of device nodes associated with the device.");
		}

		string? deviceName = null;
		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid guid) && guid == DeviceInterfaceClassGuids.Hid)
			{
				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
					deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
					usagePage == (ushort)HidUsagePage.Consumer && usageId == (ushort)HidConsumerUsage.ConsumerControl)
				{
					deviceName = deviceInterface.Id;
				}
				else
				{
					throw new InvalidOperationException("Unexpected number of device nodes associated with the device.");
				}
			}
		}

		if (deviceName is null)
		{
			throw new InvalidOperationException("Failed to identify the device interface to use.");
		}

		var stream = new HidFullDuplexStream(deviceName);
		var device = new StreamDeckDevice(stream, productId);
		try
		{
			string serialNumber = await device.GetSerialNumberAsync(cancellationToken).ConfigureAwait(false);

			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new StreamDeckDeviceDriver
				(
					device,
					friendlyName,
					productId,
					version,
					new("StreamDeck", topLevelDeviceName, $"{ElgatoVendorId:X4}:{productId:X4}", serialNumber)
				),
				null
			);
		}
		catch
		{
			await device.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private readonly StreamDeckDevice _device;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly IDeviceFeatureCollection<IGenericDeviceFeature> _genericFeatures;

	IDeviceFeatureCollection<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

	private StreamDeckDeviceDriver
	(
		StreamDeckDevice device,
		string friendlyName,
		ushort productId,
		ushort versionNumber,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_device = device;
		_productId = productId;
		_versionNumber = versionNumber;

		_genericFeatures = FeatureCollection.Create<IGenericDeviceFeature, StreamDeckDeviceDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

	public override async ValueTask DisposeAsync()
	{
		await _device.DisposeAsync().ConfigureAwait(false);
	}

	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(ElgatoVendorId, _productId, _versionNumber);

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;
}
