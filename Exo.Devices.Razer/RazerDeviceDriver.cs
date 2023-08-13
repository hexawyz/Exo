using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Razer;

// Like the Logitech driver, this will likely benefit from a refactoring of the device discovery, allowing to create drivers with different features on-demand.
// For now, it will only exactly support the features for Razer DeathAdder V2 & Dock, but it will need more flexibility to support other devices using the same protocol.
// NB: This driver relies on system drivers provided by Razer to access device features. The protocol part is still implemented here, but we need the driver to get access to the device.
[ProductId(VendorIdSource.Usb, 0x1532, 0x007C)] // Mouse
[ProductId(VendorIdSource.Usb, 0x1532, 0x007D)] // Mouse via Dongle
[ProductId(VendorIdSource.Usb, 0x1532, 0x007E)] // Dock
public sealed class RazerDeviceDriver :
	HidDriver,
	IRazerDeviceNotificationSink,
	IDeviceDriver<ILightingDeviceFeature>,
	IUnifiedLightingFeature,
	ILightingZoneEffect<DisabledEffect>,
	ILightingZoneEffect<StaticColorEffect>
{
	private static readonly Guid RazerControlDeviceInterfaceClassGuid = new Guid(0xe3be005d, 0xd130, 0x4910, 0x88, 0xff, 0x09, 0xae, 0x02, 0xf6, 0x80, 0xe9);

	private static readonly Guid DockLightingZoneGuid = new(0x5E410069, 0x0F34, 0x4DD8, 0x80, 0xDB, 0x5B, 0x11, 0xFB, 0xD4, 0x13, 0xD6);
	private static readonly Guid DeathAdderV2ProLightingZoneGuid = new(0x4D2EE313, 0xEA46, 0x4857, 0x89, 0x8C, 0x5B, 0xF9, 0x44, 0x09, 0x0A, 0x9A);

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	};

	public static async Task<RazerDeviceDriver> CreateAsync(string deviceName, ushort productId, CancellationToken cancellationToken)
	{
		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the matching HID device interfaces at once.
		var hidDeviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		// Make a device query to fetch the razer control device interface.
		var razerControlDeviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == RazerControlDeviceInterfaceClassGuid &
				Properties.System.Devices.ContainerId == containerId,
			cancellationToken
		).ConfigureAwait(false);

		if (razerControlDeviceInterfaces.Length != 1)
		{
			throw new InvalidOperationException("Expected a single device interface for Razer device control.");
		}

		string razerControlDeviceName = razerControlDeviceInterfaces[0].Id;

		// Find the top-level device by requesting devices with children.
		// The device tree should be very simple in this case, so we expect this to directly return the top level device. It would not work on more complex scenarios.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			Array.Empty<Property>(),
			Properties.System.Devices.ContainerId == containerId & Properties.System.Devices.Children.Exists(),
			cancellationToken
		).ConfigureAwait(false);

		string[] deviceNames = new string[hidDeviceInterfaces.Length + 2];
		string topLevelDeviceName = devices[0].Id;
		string? notificationDeviceName = null;

		// Set the razer control device as the first device name for now.
		deviceNames[0] = razerControlDeviceName;

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDeviceName;

		for (int i = 0; i < hidDeviceInterfaces.Length; i++)
		{
			var deviceInterface = hidDeviceInterfaces[i];
			deviceNames[i + 1] = deviceInterface.Id;

			if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage) &&
				deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId) &&
				usagePage == (ushort)HidUsagePage.GenericDesktop && usageId == 0)
			{
				using (var device = HidDevice.FromPath(deviceInterface.Id))
				{
					// TODO: Might finally be time to work on that HID descriptor part 😫
					// Also, I don't know why Windows insist on declaring the values we are looking for as button. The HID descriptor clearly indicates values between 0 and 255…
					var nodes = device.GetButtonCapabilities(NativeMethods.HidParsingReportType.Input);

					// Thanks to the button caps we can check the Report ID.
					// We are looking for the HID device interface tied to the collection with Report ID 5.
					// (Remember this relatively annoying Windows-specific stuff of splitting interfaces by top-level collection)
					if (nodes[0].ReportID == 5)
					{
						if (notificationDeviceName is not null) throw new InvalidOperationException("Found two device interfaces matching the criterion for Razer device notifications.");

						notificationDeviceName = deviceInterface.Id;
					}
				}
			}
		}

		if (notificationDeviceName is null)
		{
			throw new InvalidOperationException("The device interface for device notifications was not found.");
		}

		var transport = new RazerProtocolTransport(Device.OpenHandle(razerControlDeviceName, DeviceAccess.None));

		string serialNumber = transport.GetSerialNumber();

		return new RazerDeviceDriver
		(
			transport,
			new(notificationDeviceName),
			productId == 0x007E ? DeviceCategory.Usb : DeviceCategory.Mouse,
			productId == 0x007E ? DockLightingZoneGuid : DeathAdderV2ProLightingZoneGuid,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName,
			new("RazerDevice", topLevelDeviceName, $"Razer_Device_{productId:X4}", serialNumber)
		);
	}

	private readonly RazerProtocolTransport _transport;
	private readonly RazerDeviceNotificationWatcher _watcher;
	private ILightingEffect _appliedEffect;
	private ILightingEffect _currentEffect;
	private readonly Guid _lightingZoneId;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public override DeviceCategory DeviceCategory { get; }

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	private RazerDeviceDriver(
		RazerProtocolTransport transport,
		HidFullDuplexStream notificationStream,
		DeviceCategory deviceCategory,
		Guid lightingZoneId,
		ImmutableArray<string> deviceNames,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(deviceNames, friendlyName, configurationKey)
	{
		_transport = transport;
		_watcher = new(notificationStream, this);
		_appliedEffect = DisabledEffect.SharedInstance;
		_currentEffect = DisabledEffect.SharedInstance;
		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, RazerDeviceDriver, IUnifiedLightingFeature>(this);
		_allFeatures = FeatureCollection.Create<IDeviceFeature, RazerDeviceDriver, IUnifiedLightingFeature>(this);
		DeviceCategory = deviceCategory;
		_lightingZoneId = lightingZoneId;
	}

	public override ValueTask DisposeAsync()
	{
		_transport.Dispose();
		return ValueTask.CompletedTask;
	}

	public void OnDeviceArrival()
	{
		ApplyChanges(_appliedEffect);
	}

	public void OnDeviceRemoval()
	{
	}

	// TODO
	bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

	ValueTask IUnifiedLightingFeature.ApplyChangesAsync()
	{
		ApplyChanges(_appliedEffect = _currentEffect);
		return ValueTask.CompletedTask;
	}

	private void ApplyChanges(ILightingEffect effect)
	{
		switch (_currentEffect)
		{
		case DisabledEffect: _transport.SetStaticColor(default); break;
		case StaticColorEffect staticColorEffect: _transport.SetStaticColor(staticColorEffect.Color); break;
		}
	}

	// TODO: Devices can support multiple lighting zones OR a single zone. We must support both scenarios.
	Guid ILightingZone.ZoneId => _lightingZoneId;

	ILightingEffect ILightingZone.GetCurrentEffect() => _currentEffect;

	void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => _currentEffect = DisabledEffect.SharedInstance;
	void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => _currentEffect = effect;

	bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => _currentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => _currentEffect.TryGetEffect(out effect);
}
