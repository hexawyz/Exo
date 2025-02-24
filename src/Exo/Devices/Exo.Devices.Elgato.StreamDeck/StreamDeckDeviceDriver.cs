using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.EmbeddedMonitors;
using Exo.Features.Monitors;
using Exo.Features.PowerManagement;
using Exo.Features.UserInput;
using Exo.Images;
using Exo.Internal;
using Exo.Monitors;

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
public sealed partial class StreamDeckDeviceDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<IEmbeddedMonitorDeviceFeature>,
	IDeviceDriver<IPowerManagementDeviceFeature>,
	IDeviceDriver<IUserInputDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IEmbeddedMonitorControllerFeature,
	IMonitorBrightnessFeature,
	IIdleSleepTimerFeature,
	IRemappableButtonsFeature,
	IInterceptedButtonsFeature
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
			var deviceInfo = await device.GetDeviceInfoAsync(cancellationToken).ConfigureAwait(false);
			uint idleSleepDelay = await device.GetSleepTimerAsync(cancellationToken).ConfigureAwait(false);

			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new StreamDeckDeviceDriver
				(
					device,
					StreamDeckXlButtonIds,
					deviceInfo,
					idleSleepDelay,
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
	private readonly ꂓ _buttonIds;
	private readonly StreamDeckDeviceInfo _deviceInfo;
	private event ButtonEventHandler? ButtonDown;
	private event ButtonEventHandler? ButtonUp;
	private uint _idleSleepDelay;
	private UInt128 _lastImageId;
	private readonly ushort _productId;
	private readonly ushort _versionNumber;
	private readonly Button[] _buttons;
	private readonly ImmutableArray<RemappableButtonDefinition> _buttonDefinitions;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureSet<IUserInputDeviceFeature> _userInputFeatures;
	private readonly IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> _embeddedMonitorFeatures;
	private readonly IDeviceFeatureSet<IPowerManagementDeviceFeature> _powerManagementFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	IDeviceFeatureSet<IEmbeddedMonitorDeviceFeature> IDeviceDriver<IEmbeddedMonitorDeviceFeature>.Features => _embeddedMonitorFeatures;
	IDeviceFeatureSet<IUserInputDeviceFeature> IDeviceDriver<IUserInputDeviceFeature>.Features => _userInputFeatures;
	IDeviceFeatureSet<IPowerManagementDeviceFeature> IDeviceDriver<IPowerManagementDeviceFeature>.Features => _powerManagementFeatures;

	private StreamDeckDeviceDriver
	(
		StreamDeckDevice device,
		ꂓ buttonIds,
		StreamDeckDeviceInfo deviceInfo,
		uint idleSleepDelay,
		string friendlyName,
		ushort productId,
		ushort versionNumber,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_device = device;
		_buttonIds = buttonIds;
		_deviceInfo = deviceInfo;
		_idleSleepDelay = idleSleepDelay;
		_productId = productId;
		_versionNumber = versionNumber;

		var buttons = new Button[deviceInfo.ButtonCount];
		var buttonDefinitions = new RemappableButtonDefinition[deviceInfo.ButtonCount];
		for (int i = 0; i < buttons.Length; i++)
		{
			buttons[i] = new(this, (byte)i);
			buttonDefinitions[i] = new(new(HidUsagePage.VendorDefinedFF00, (ushort)i), ButtonCapabilities.HasCustomDisplay, StreamDeckXlButtonIds[i], []);
		}
		_buttons = buttons;
		_buttonDefinitions = ImmutableCollectionsMarshal.AsImmutableArray(buttonDefinitions);

		_genericFeatures = FeatureSet.Create<IGenericDeviceFeature, StreamDeckDeviceDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this);
		_monitorFeatures = FeatureSet.Empty<IMonitorDeviceFeature>();
		_embeddedMonitorFeatures = FeatureSet.Create<IEmbeddedMonitorDeviceFeature, StreamDeckDeviceDriver, IEmbeddedMonitorControllerFeature>(this);
		_userInputFeatures = FeatureSet.Create<IUserInputDeviceFeature, StreamDeckDeviceDriver, IRemappableButtonsFeature, IInterceptedButtonsFeature>(this);
		_powerManagementFeatures = FeatureSet.Create<IPowerManagementDeviceFeature, StreamDeckDeviceDriver, IIdleSleepTimerFeature>(this);

		_device.ButtonStateChanged += OnButtonStateChanged;
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

	public override async ValueTask DisposeAsync()
	{
		await _device.DisposeAsync().ConfigureAwait(false);
	}

	// TODO: This event is currently transmitted synchronously. Proper state management will require queuing of events.
	// This probably doesn't need to be done here, but to be revisted later, as other device events (e.g. battery) may impose different semantics overall.
	// Probably, if we can document all driver events as single-writer, in-order notifications that can be processed very quickly, we would avoid more problems.
	// As the drivers are actually always used in code that has a deterministic behavior (one that we can choose), this is maybe not that unreasonable.
	// IIRC problems arise when we want time coherence between different events… Which is where things may need to change.
	private void OnButtonStateChanged(StreamDeckDevice device, uint previousButtons, uint currentButtons)
	{
		nuint changed = previousButtons ^ currentButtons;

		// Process ButtonUp events first
		{
			nuint up = previousButtons & changed;
			if (up != 0 && ButtonUp is { } buttonUp)
			{
				DispatchButtonEvents(up, buttonUp);
			}
		}

		// Process ButtonDown events second
		{
			nuint down = currentButtons & changed;
			if (down != 0 && ButtonDown is { } buttonDown)
			{
				DispatchButtonEvents(down, buttonDown);
			}
		}
	}

	// The logic for dispatching up/down events is obviously the same.
	// We just find the indices of all buttons that have been pressed or released and invoke the handler.
	private void DispatchButtonEvents(nuint mask, ButtonEventHandler handler)
	{
		nuint buttonIndex = 0;
		while (true)
		{
			int shift = BitOperations.TrailingZeroCount(mask);
			buttonIndex += (nuint)shift;

			try
			{
				handler(this, new ButtonId(HidUsagePage.VendorDefinedFF00, (ushort)buttonIndex));
			}
			catch
			{
				// TODO: Log ?
			}

			if ((mask = mask >>> (shift + 1)) == 0) break;
		}
	}

	private Size ButtonImageSize => new(_deviceInfo.ButtonImageWidth, _deviceInfo.ButtonImageHeight);

	DeviceId IDeviceIdFeature.DeviceId => DeviceId.ForUsb(ElgatoVendorId, _productId, _versionNumber);

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	// Elgato Stream Deck actually only allows setting the delay up to two hours, but the device can do much more.
	// The maximum we can set here will thus always be arbitrary, but for now, setting values that are too big will make the UI unusable or even break it. (Broken for uint.MaxValue)
	TimeSpan IIdleSleepTimerFeature.MaximumIdleTime => TimeSpan.FromTicks(2 * TimeSpan.TicksPerHour);
	TimeSpan IIdleSleepTimerFeature.IdleTime => TimeSpan.FromTicks(_idleSleepDelay * TimeSpan.TicksPerSecond);

	async Task IIdleSleepTimerFeature.SetIdleTimeAsync(TimeSpan idleTime, CancellationToken cancellationToken)
	{
		if ((ulong)(idleTime.Ticks - 5) > 8 * TimeSpan.TicksPerHour) throw new ArgumentOutOfRangeException(nameof(idleTime));
		uint idleSleepDelay = (uint)((ulong)idleTime.Ticks / TimeSpan.TicksPerSecond);
		await _device.SetSleepTimerAsync(idleSleepDelay, cancellationToken).ConfigureAwait(false);
		_idleSleepDelay = idleSleepDelay;
	}

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

		// Bitmap seems to not work at all. Until I find a way to understand how colors are mapped, it is better to disable it. (e.g. black would give dark purple, white would give maroon)
		EmbeddedMonitorInformation IEmbeddedMonitor.MonitorInformation
			=> new(MonitorShape.Square, ImageRotation.Rotate180, _driver.ButtonImageSize, PixelFormat.B8G8R8, /*ImageFormats.Bitmap | */ImageFormats.Jpeg, false);

		async ValueTask IEmbeddedMonitor.SetImageAsync(UInt128 imageId, ImageFormat imageFormat, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
		{
			if (imageFormat is not (ImageFormat.Bitmap or ImageFormat.Jpeg))
			{
				throw new ArgumentOutOfRangeException(nameof(imageFormat));
			}
			// We basically can avoid sending a lengthy transfer over to the device if two consecutive images are the same.
			// The write buffer will still contain the data from the last upload.
			await _driver._device.SetKeyImageDataAsync(_keyIndex, imageId == _driver._lastImageId ? Array.Empty<byte>() : data, cancellationToken);
			_driver._lastImageId = imageId;
		}
	}

	ImmutableArray<RemappableButtonDefinition> IRemappableButtonsFeature.Buttons => _buttonDefinitions;

	// TODO: We'll see what will be the requirement for implementation of this method later. Should it always be called with an action of "intercept" or should it not be called at all?
	void IRemappableButtonsFeature.SetAction(ButtonId buttonId, Guid actionId) => throw new NotSupportedException();

	void IRemappableButtonsFeature.ResetButton(ButtonId buttonId) { }

	ValueTask IRemappableButtonsFeature.ApplyChangesAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;

	event ButtonEventHandler IInterceptedButtonsFeature.ButtonDown
	{
		add => ButtonDown += value;
		remove => ButtonDown -= value;
	}

	event ButtonEventHandler IInterceptedButtonsFeature.ButtonUp
	{
		add => ButtonUp += value;
		remove => ButtonUp -= value;
	}
}
