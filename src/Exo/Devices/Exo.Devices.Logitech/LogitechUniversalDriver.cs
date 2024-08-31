using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus;
using Exo.Discovery;
using Exo.Features;
using Microsoft.Extensions.Logging;
using FeatureAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.DeviceType;
using RegisterAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.DeviceType;

namespace Exo.Devices.Logitech;

// This driver is a catch-all for Logitech devices. On first approximation, they should all implement the proprietary HID++ protocol.
public abstract partial class LogitechUniversalDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IPowerManagementDeviceFeature>,
	IDeviceSerialNumberFeature,
	IDeviceIdFeature
{
	private const int LogitechUsbVendorId = 0x046D;
	private const string LogitechDriverKey = "logi";

	// Hardcoded value for the software ID. Hoping it will not conflict with anything still in use today.
	private const int SoftwareId = 3;

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
	[VendorId(VendorIdSource.Usb, LogitechUsbVendorId)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
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
		// Those virtual devices are excluded. The discovery will gracefully ignore them unless they are handled by another factory method.
		if (productId == 0xC231) return null;
		if (productId == 0xC232) return null;

		HidPlusPlusProtocolFlavor protocolFlavor = default;
		string? shortInterfaceName = null;
		string? longInterfaceName = null;
		string? veryLongInterfaceName = null;
		SupportedReports discoveredReports = 0;
		SupportedReports expectedReports = 0;
		DeviceIdSource deviceIdSource = DeviceIdSource.Unknown;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid classGuid) || classGuid != DeviceInterfaceClassGuids.Hid)
			{
				continue;
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
					var collectionDescriptor = await hid.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);
					if (!(collectionDescriptor.InputReports.MaximumReportLength == 64 && collectionDescriptor.OutputReports.MaximumReportLength == 64 && collectionDescriptor.FeatureReports.MaximumReportLength == 0)) continue;
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
			new(deviceIdSource, productId),
			SoftwareId,
			friendlyName,
			new TimeSpan(1 * TimeSpan.TicksPerSecond),
			loggerFactory,
			cancellationToken
		);

		// HID++ devices will expose multiple interfaces, each with their own top-level collection.
		// Typically for Mouse/Keyboard/Receiver, these would be 00: Boot Keyboard, 01: Input stuff, 02: HID++/DJ
		// We want to take the device that is just above all these interfaces. So, typically the name of a raw USB or BT device.
		// TODO: Register access devices probably need a mapping between USB/BT IDs and WPID ? (If there are HID++ 1.0 multi transport devices. Should be added in the lower level code)
		var configurationKey = new DeviceConfigurationKey(LogitechDriverKey, topLevelDeviceName, $"{LogitechUsbVendorId:X4}:{hppDevice.MainProductId:X4}", hppDevice.SerialNumber);

		var driver = CreateDriver(driverRegistry, loggerFactory, hppDevice, configurationKey, version);

		// TODO: Watching devices should probably only done once the driver has been registered to the driver manager.
		if (driver is SystemDrivers.Receiver receiver)
		{
			try
			{
				await receiver.StartWatchingDevicesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				await receiver.DisposeAsync();
				throw;
			}
		}

		return new DriverCreationResult<SystemDevicePath>(keys, driver, null);
	}

	private static LogitechUniversalDriver CreateDriver
	(
		Optional<IDriverRegistry> driverRegistry,
		ILoggerFactory loggerFactory,
		HidPlusPlusDevice hppDevice,
		DeviceConfigurationKey configurationKey,
		ushort versionNumber
	)
	{
		switch (hppDevice)
		{
		case HidPlusPlusDevice.UnifyingReceiver unifyingReceiver:
			return new SystemDrivers.UnifyingReceiver(unifyingReceiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.UnifyingReceiver>(), configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.BoltReceiver boltReceiver:
			return new SystemDrivers.BoltReceiver(boltReceiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.BoltReceiver>(), configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.RegisterAccessReceiver receiver:
			return new SystemDrivers.Receiver(receiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.Receiver>(), configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.RegisterAccessDirect rapDirect:
			driverRegistry.Dispose();
			return rapDirect.DeviceType switch
			{
				RegisterAccessDeviceType.Keyboard => new SystemDrivers.RegisterAccessDirectKeyboard(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectKeyboard>(), configurationKey, versionNumber),
				RegisterAccessDeviceType.Mouse => new SystemDrivers.RegisterAccessDirectMouse(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectMouse>(), configurationKey, versionNumber),
				_ => new SystemDrivers.RegisterAccessDirectGeneric(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectGeneric>(), configurationKey, versionNumber, GetDeviceCategory(rapDirect.DeviceType)),
			};
		case HidPlusPlusDevice.FeatureAccessDirect fapDirect:
			driverRegistry.Dispose();
			return fapDirect.DeviceType switch
			{
				FeatureAccessDeviceType.Keyboard => new SystemDrivers.FeatureAccessDirectKeyboard(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectKeyboard>(), configurationKey, versionNumber),
				FeatureAccessDeviceType.Mouse => new SystemDrivers.FeatureAccessDirectMouse(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectMouse>(), configurationKey, versionNumber),
				_ => new SystemDrivers.FeatureAccessDirectGeneric(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectGeneric>(), configurationKey, versionNumber, GetDeviceCategory(fapDirect.DeviceType)),
			};
		default:
			throw new InvalidOperationException("Unsupported device type.");
		};
	}

	private static LogitechUniversalDriver CreateChildDriver(string parentDeviceName, HidPlusPlusDevice hppDevice, ILoggerFactory loggerFactory)
	{
		DeviceConfigurationKey configurationKey;
		switch (hppDevice)
		{
		case HidPlusPlusDevice.RegisterAccessThroughReceiver rap:
			configurationKey = new DeviceConfigurationKey(LogitechDriverKey, $"{parentDeviceName}#{rap.DeviceIndex}", $"{LogitechUsbVendorId:X4}:{hppDevice.MainProductId:X4}", hppDevice.SerialNumber);
			return rap.DeviceType switch
			{
				RegisterAccessDeviceType.Keyboard => new BaseDrivers.RegisterAccessThroughReceiverKeyboard(rap, loggerFactory.CreateLogger<BaseDrivers.RegisterAccessThroughReceiverKeyboard>(), configurationKey, 0xFFFF),
				RegisterAccessDeviceType.Mouse => new BaseDrivers.RegisterAccessThroughReceiverMouse(rap, loggerFactory.CreateLogger<BaseDrivers.RegisterAccessThroughReceiverMouse>(), configurationKey, 0xFFFF),
				_ => new BaseDrivers.RegisterAccessThroughReceiverGeneric(rap, loggerFactory.CreateLogger<BaseDrivers.RegisterAccessThroughReceiverGeneric>(), configurationKey, 0xFFFF, GetDeviceCategory(rap.DeviceType)),
			};
		case HidPlusPlusDevice.FeatureAccessThroughReceiver fap:
			configurationKey = new DeviceConfigurationKey(LogitechDriverKey, $"{parentDeviceName}#{fap.DeviceIndex}", $"{LogitechUsbVendorId:X4}:{hppDevice.MainProductId:X2}", hppDevice.SerialNumber);
			return fap.DeviceType switch
			{
				FeatureAccessDeviceType.Keyboard => new BaseDrivers.FeatureAccessThroughReceiverKeyboard(fap, loggerFactory.CreateLogger<BaseDrivers.FeatureAccessThroughReceiverKeyboard>(), configurationKey, 0xFFFF),
				FeatureAccessDeviceType.Mouse => new BaseDrivers.FeatureAccessThroughReceiverMouse(fap, loggerFactory.CreateLogger<BaseDrivers.FeatureAccessThroughReceiverMouse>(), configurationKey, 0xFFFF),
				_ => new BaseDrivers.FeatureAccessThroughReceiverGeneric(fap, loggerFactory.CreateLogger<BaseDrivers.FeatureAccessThroughReceiverGeneric>(), configurationKey, 0xFFFF, GetDeviceCategory(fap.DeviceType)),
			};
		default:
			throw new InvalidOperationException("Unsupported device type.");
		};
	}

	private static DeviceCategory GetDeviceCategory(RegisterAccessDeviceType deviceType)
		=> deviceType switch
		{
			RegisterAccessDeviceType.Keyboard => DeviceCategory.Keyboard,
			RegisterAccessDeviceType.Mouse => DeviceCategory.Mouse,
			RegisterAccessDeviceType.Numpad => DeviceCategory.Numpad,
			RegisterAccessDeviceType.Trackball => DeviceCategory.Mouse,
			RegisterAccessDeviceType.Touchpad => DeviceCategory.Touchpad,
			_ => DeviceCategory.Touchpad
		};

	private static DeviceCategory GetDeviceCategory(FeatureAccessDeviceType deviceType)
		=> deviceType switch
		{
			FeatureAccessDeviceType.Keyboard => DeviceCategory.Keyboard,
			FeatureAccessDeviceType.Numpad => DeviceCategory.Keyboard,
			FeatureAccessDeviceType.Mouse => DeviceCategory.Mouse,
			FeatureAccessDeviceType.Trackpad => DeviceCategory.Touchpad,
			FeatureAccessDeviceType.Trackball => DeviceCategory.Mouse,
			FeatureAccessDeviceType.Headset => DeviceCategory.Headset,
			FeatureAccessDeviceType.Webcam => DeviceCategory.Webcam,
			FeatureAccessDeviceType.SteeringWheel => DeviceCategory.Gamepad,
			FeatureAccessDeviceType.Joystick => DeviceCategory.Gamepad,
			FeatureAccessDeviceType.Gamepad => DeviceCategory.Gamepad,
			FeatureAccessDeviceType.Speaker => DeviceCategory.Speaker,
			FeatureAccessDeviceType.Microphone => DeviceCategory.Microphone,
			FeatureAccessDeviceType.IlluminationLight => DeviceCategory.Lighting,
			FeatureAccessDeviceType.CarSimPedals => DeviceCategory.Gamepad,
			_ => DeviceCategory.Other,
		};

	private static string InferDeviceName(HidPlusPlusDevice device)
		=> device switch
		{
			HidPlusPlusDevice.RegisterAccess rap => InferDeviceName(rap),
			HidPlusPlusDevice.FeatureAccess fap => InferDeviceName(fap),
			_ => throw new InvalidOperationException("Unsupported device type.")
		};

	private static string InferDeviceName(HidPlusPlusDevice.RegisterAccess device)
		=> device.DeviceType is RegisterAccessDeviceType.Unknown ? "HID++ 1.0 device" : device.DeviceType.ToString();

	private static string InferDeviceName(HidPlusPlusDevice.FeatureAccess device)
		=> device.DeviceType is FeatureAccessDeviceType.Unknown ? "HID++ 2.0 device" : device.DeviceType.ToString();

	private readonly HidPlusPlusDevice _device;
	// The logger will use the category of the derived class… I don't believe we want to have one logger reference for each class in the class hierarchy.
	private readonly ILogger<LogitechUniversalDriver> _logger;
	private readonly ushort _versionNumber;
	private readonly IDeviceFeatureSet<IPowerManagementDeviceFeature> _powerManagementFeatures;
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

	IDeviceFeatureSet<IPowerManagementDeviceFeature> IDeviceDriver<IPowerManagementDeviceFeature>.Features => _powerManagementFeatures;
	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;

	private bool HasSerialNumber => ConfigurationKey.UniqueId is { Length: not 0 };

	string IDeviceSerialNumberFeature.SerialNumber
		=> HasSerialNumber ?
			ConfigurationKey.UniqueId! :
			throw new NotSupportedException("This device does not support the Serial Number feature.");

	DeviceId IDeviceIdFeature.DeviceId => new(_device.MainDeviceId.Source, VendorIdSource.Usb, LogitechUsbVendorId, _device.MainDeviceId.ProductId, _versionNumber);

	protected LogitechUniversalDriver
	(
		HidPlusPlusDevice device,
		ILogger<LogitechUniversalDriver> logger,
		DeviceConfigurationKey configurationKey,
		ushort versionNumber
	)
		: base(device.FriendlyName ?? InferDeviceName(device), configurationKey)
	{
		_device = device;
		_logger = logger;
		_versionNumber = versionNumber;
		_powerManagementFeatures = CreatePowerManagementDeviceFeatures();
		_genericFeatures = CreateGenericFeatures();
	}

	protected virtual IDeviceFeatureSet<IPowerManagementDeviceFeature> CreatePowerManagementDeviceFeatures()
		=> FeatureSet.Empty<IPowerManagementDeviceFeature>();

	// NB: Called from the main constructor, so derived classes need to make sure they are not missing any information at that point.
	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
		=> HasSerialNumber ?
			FeatureSet.Create<IGenericDeviceFeature, LogitechUniversalDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, LogitechUniversalDriver, IDeviceIdFeature>(this);

	// NB: This calls DisposeAsync on all devices, but child devices will not actually be disposed, as they are managed by their parent.
	public override ValueTask DisposeAsync() => _device.DisposeAsync();
}
