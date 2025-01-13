using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Monitors;
using Exo.Images;
using Exo.Monitors;
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
public sealed class StreamDeckDeviceDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IEmbeddedMonitorControllerFeature,
	IMonitorBrightnessFeature
{
	private static readonly Guid[] StreamDeckXlButtonIds = [
		new(0x903959D8, 0x4449, 0x4081, 0x92, 0x24, 0x60, 0x54, 0x55, 0x26, 0xAB, 0xE6),
		new(0xCF380166, 0x0805, 0x4BE7, 0xBF, 0xAD, 0x10, 0x44, 0x9E, 0xD1, 0x63, 0x9E),
		new(0xE412EB19, 0x58A1, 0x4F26, 0xAE, 0x7A, 0xFE, 0x8D, 0xF3, 0x18, 0xF5, 0x85),
		new(0x82994C85, 0x3CEF, 0x4093, 0xA0, 0x31, 0x0B, 0xF8, 0x81, 0x8A, 0x71, 0x1F),
		new(0x211D7055, 0xAFCE, 0x4EA6, 0x90, 0xBE, 0x36, 0xDE, 0xAB, 0xBE, 0x0F, 0xBC),
		new(0xD7ED1BE9, 0xBE25, 0x4007, 0x84, 0x3E, 0x02, 0xD5, 0xEF, 0xC4, 0xE2, 0x9E),
		new(0xB3D52EEB, 0x3833, 0x4E84, 0xAF, 0x91, 0x19, 0xA4, 0xE7, 0x92, 0xAB, 0x44),
		new(0x3E5F4DFF, 0x3723, 0x4DFB, 0xA4, 0x56, 0xFA, 0x84, 0xA0, 0x23, 0x2E, 0xC5),
		new(0xC94DDD1D, 0x6D5C, 0x4295, 0xB5, 0x9F, 0x84, 0x5B, 0xBF, 0x6F, 0xD5, 0x40),
		new(0x3D9F4285, 0x8BD1, 0x423B, 0xB7, 0xC4, 0xD8, 0x3C, 0x6A, 0x72, 0x57, 0x18),
		new(0xAF42B08F, 0x409C, 0x4B96, 0xBE, 0x89, 0xB9, 0xB8, 0xA3, 0x32, 0x85, 0x0C),
		new(0x7608B456, 0xA90B, 0x49AF, 0x96, 0xA2, 0x94, 0x78, 0xBC, 0x7C, 0x77, 0x70),
		new(0x9DEBCE69, 0x469A, 0x480A, 0xBE, 0x34, 0x46, 0xA7, 0xA6, 0x10, 0x61, 0x78),
		new(0x0F9C3B6E, 0xAF86, 0x4A04, 0x89, 0x13, 0x39, 0x33, 0xFD, 0xD6, 0xD9, 0x4A),
		new(0xC8A60D4B, 0xB6D8, 0x40AF, 0x82, 0xB4, 0xCF, 0xA8, 0xB2, 0xD0, 0x8F, 0x6E),
		new(0xD7CE48B8, 0x5807, 0x4F28, 0xA6, 0xEA, 0x2E, 0x42, 0x26, 0xC5, 0x5B, 0x30),
		new(0xFC472091, 0x5D34, 0x4FC1, 0xA7, 0xA9, 0xA6, 0xF6, 0xC8, 0xCD, 0x4C, 0x47),
		new(0x656C6B17, 0x371C, 0x4D4F, 0x92, 0x78, 0xCC, 0xF2, 0xF3, 0xFC, 0xD7, 0xA5),
		new(0x72FCC17E, 0xF725, 0x4D2A, 0xB3, 0x7A, 0xF4, 0x31, 0x26, 0x91, 0x05, 0x11),
		new(0x363A6A93, 0xAAB4, 0x4CE5, 0xB7, 0xF0, 0x12, 0x4E, 0xE2, 0xDD, 0x23, 0xA4),
		new(0x3C603EF9, 0xB96C, 0x4D75, 0x9B, 0x6C, 0xD6, 0xA7, 0x2A, 0x55, 0xCC, 0x7C),
		new(0xF36CE9EC, 0xD225, 0x45C9, 0xB6, 0xF2, 0x21, 0x5C, 0x35, 0x6D, 0xC3, 0xA9),
		new(0x9733A887, 0x7758, 0x4928, 0xB1, 0x08, 0x5A, 0x44, 0x08, 0xCD, 0x82, 0xC6),
		new(0x28EEED2E, 0x9099, 0x4E50, 0x95, 0xCC, 0x39, 0xDC, 0xB3, 0x5A, 0x66, 0x78),
		new(0xFC135451, 0x54D4, 0x42C8, 0x9B, 0x15, 0xCC, 0x59, 0x65, 0xFB, 0xA4, 0xA0),
		new(0xC3DDEF57, 0x9D98, 0x4048, 0x93, 0xEE, 0xCA, 0x5F, 0x29, 0xEE, 0xE7, 0x93),
		new(0xC0951F45, 0xD7B1, 0x44C7, 0x95, 0x74, 0x10, 0xEE, 0x90, 0x19, 0xC7, 0x25),
		new(0xD33FFC43, 0xCDE4, 0x4F06, 0x8F, 0xB2, 0x3F, 0x91, 0x23, 0x6A, 0xC4, 0xDB),
		new(0xA61978BF, 0x8AD6, 0x42DE, 0xBB, 0xA3, 0x0A, 0x31, 0x6D, 0x98, 0x90, 0x2F),
		new(0xA75F7663, 0xEAB1, 0x4510, 0xBE, 0x6B, 0x49, 0x2C, 0x0B, 0x78, 0x3A, 0x7F),
		new(0x01AA7856, 0xC94B, 0x4587, 0xA5, 0x31, 0x3B, 0xEA, 0xB2, 0xB3, 0x04, 0x26),
		new(0x63AA0DA1, 0x17D9, 0x482E, 0x92, 0xAE, 0xD0, 0x3A, 0x89, 0x99, 0xA7, 0x65),
	];

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
			var deviceInfo = await device.GetDeviceInfoAsync(cancellationToken).ConfigureAwait(false);

			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new StreamDeckDeviceDriver
				(
					device,
					StreamDeckXlButtonIds,
					deviceInfo,
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
	private readonly Guid[] _buttonIds;
	private readonly StreamDeckDeviceInfo _deviceInfo;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly Button[] _buttons;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	private StreamDeckDeviceDriver
	(
		StreamDeckDevice device,
		Guid[] buttonIds,
		StreamDeckDeviceInfo deviceInfo,
		string friendlyName,
		ushort productId,
		ushort versionNumber,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_device = device;
		_buttonIds = buttonIds;
		_deviceInfo = deviceInfo;
		_productId = productId;
		_versionNumber = versionNumber;

		var buttons = new Button[deviceInfo.ButtonCount];
		for (int i = 0; i < buttons.Length; i++)
		{
			buttons[i] = new(this, (byte)i);
		}
		_buttons = buttons;

		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, StreamDeckDeviceDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this);
		_monitorFeatures = FeatureSet.Create<IMonitorDeviceFeature, StreamDeckDeviceDriver, IEmbeddedMonitorControllerFeature>(this);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

	public override async ValueTask DisposeAsync()
	{
		await _device.DisposeAsync().ConfigureAwait(false);
	}

	private Size ButtonImageSize => new(_deviceInfo.ButtonImageWidth, _deviceInfo.ButtonImageHeight);

	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(ElgatoVendorId, _productId, _versionNumber);

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<IEmbeddedMonitor> IEmbeddedMonitorControllerFeature.EmbeddedMonitors => ImmutableCollectionsMarshal.AsImmutableArray(Unsafe.As<IEmbeddedMonitor[]>(_buttons));

	// TODO: Must be able to read the value from the device.
	ValueTask<ContinuousValue> IContinuousVcpFeature.GetValueAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
	ValueTask IContinuousVcpFeature.SetValueAsync(ushort value, CancellationToken cancellationToken) => throw new NotImplementedException();

	private sealed class Button : IEmbeddedMonitor
	{
		private readonly StreamDeckDeviceDriver _driver;
		private readonly byte _keyIndex;

		public Button(StreamDeckDeviceDriver driver, byte buttonId)
		{
			_driver = driver;
			_keyIndex = buttonId;
		}

		Guid IEmbeddedMonitor.MonitorId => _driver._buttonIds[_keyIndex];
		MonitorShape IEmbeddedMonitor.Shape => MonitorShape.Square;
		Size IEmbeddedMonitor.ImageSize => _driver.ButtonImageSize;
		PixelFormat IEmbeddedMonitor.PixelFormat => PixelFormat.R8G8B8X8;
		ImageFormats IEmbeddedMonitor.SupportedImageFormats { get; }
	}
}
