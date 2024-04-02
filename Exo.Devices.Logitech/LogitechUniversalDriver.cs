using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.KeyboardFeatures;
using Microsoft.Extensions.Logging;
using BacklightState = Exo.Features.KeyboardFeatures.BacklightState;
using FeatureAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.DeviceType;
using RegisterAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.DeviceType;

namespace Exo.Devices.Logitech;

// This driver is a catch-all for Logitech devices. On first approximation, they should all implement the proprietary HID++ protocol.
public abstract class LogitechUniversalDriver :
	Driver,
	IDeviceDriver<IGenericDeviceFeature>,
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
	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;

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
		_genericFeatures = CreateGenericFeatures();
	}

	// NB: Called from the main constructor, so derived classes need to make sure they are not missing any information at that point.
	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
		=> HasSerialNumber ?
			FeatureSet.Create<IGenericDeviceFeature, LogitechUniversalDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, LogitechUniversalDriver, IDeviceIdFeature>(this);

	// NB: This calls DisposeAsync on all devices, but child devices will not actually be disposed, as they are managed by their parent.
	public override ValueTask DisposeAsync() => _device.DisposeAsync();

	private abstract class RegisterAccess : LogitechUniversalDriver
	{
		public RegisterAccess(HidPlusPlusDevice.RegisterAccess device, ILogger<RegisterAccess> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	private abstract class FeatureAccess : LogitechUniversalDriver, IBatteryStateDeviceFeature, IKeyboardBacklightFeature, IKeyboardLockKeysFeature
	{
		public FeatureAccess(HidPlusPlusDevice.FeatureAccess device, ILogger<FeatureAccess> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
			if (HasBattery) device.BatteryChargeStateChanged += OnBatteryChargeStateChanged;

			if (HasBacklight) device.BacklightStateChanged += OnBacklightStateChanged;

			if (HasLockKeys) device.LockKeysChanged += OnLockKeysChanged;
		}

		protected override IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures()
			=> HasSerialNumber ?
				HasBattery ?
					FeatureSet.Create<IGenericDeviceFeature, FeatureAccess, IDeviceIdFeature, IDeviceSerialNumberFeature, IBatteryStateDeviceFeature>(this) :
					FeatureSet.Create<IGenericDeviceFeature, FeatureAccess, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
				HasBattery ?
					FeatureSet.Create<IGenericDeviceFeature, FeatureAccess, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
					FeatureSet.Create<IGenericDeviceFeature, FeatureAccess, IDeviceIdFeature>(this);

		public override ValueTask DisposeAsync()
		{
			Device.BatteryChargeStateChanged -= OnBatteryChargeStateChanged;
			return base.DisposeAsync();
		}

		private static BatteryState BuildBatteryState(BatteryPowerState batteryPowerState)
			=> new()
			{
				Level = batteryPowerState.BatteryLevel / 100f,
				BatteryStatus = batteryPowerState.ChargeStatus switch
				{
					BatteryChargeStatus.Discharging => BatteryStatus.Discharging,
					BatteryChargeStatus.Charging => BatteryStatus.Charging,
					BatteryChargeStatus.ChargingNearlyComplete => BatteryStatus.ChargingNearlyComplete,
					BatteryChargeStatus.ChargingComplete => BatteryStatus.ChargingComplete,
					BatteryChargeStatus.ChargingError => BatteryStatus.Error,
					BatteryChargeStatus.InvalidBatteryType => BatteryStatus.Invalid,
					BatteryChargeStatus.BatteryTooHot => BatteryStatus.TooHot,
					_ => BatteryStatus.Error,
				},
				ExternalPowerStatus =
					((batteryPowerState.ExternalPowerStatus & BatteryExternalPowerStatus.IsConnected) != 0 ? ExternalPowerStatus.IsConnected : 0) |
					((batteryPowerState.ExternalPowerStatus & BatteryExternalPowerStatus.IsChargingBelowOptimalSpeed) != 0 ? ExternalPowerStatus.IsSlowCharger : 0)
			};

		private static BacklightState BuildBacklightState(DeviceTools.Logitech.HidPlusPlus.BacklightState backlightState)
			=> new()
			{
				CurrentLevel = backlightState.CurrentLevel,
				MaximumLevel = (byte)(backlightState.LevelCount - 1),
			};

		private void OnBatteryChargeStateChanged(HidPlusPlusDevice.FeatureAccess device, BatteryPowerState batteryPowerState)
		{
			if (BatteryStateChanged is { } batteryStateChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							batteryStateChanged.Invoke(this, BuildBatteryState(batteryPowerState));
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverBatteryStateChangedError(ex);
						}
					}
				);
			}
		}

		private void OnLockKeysChanged(HidPlusPlusDevice.FeatureAccess device, DeviceTools.Logitech.HidPlusPlus.LockKeys lockKeys)
		{
			if (LockKeysChanged is { } lockKeysChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							lockKeysChanged.Invoke(this, (LockKeys)(byte)lockKeys);
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverLockKeysChangedError(ex);
						}
					}
				);
			}
		}

		private void OnBacklightStateChanged(HidPlusPlusDevice.FeatureAccess device, DeviceTools.Logitech.HidPlusPlus.BacklightState backlightState)
		{
			if (BacklightStateChanged is { } backlightStateChanged)
			{
				_ = Task.Run
				(
					() =>
					{
						try
						{
							backlightStateChanged.Invoke(this, BuildBacklightState(backlightState));
						}
						catch (Exception ex)
						{
							_logger.LogitechUniversalDriverBacklightStateChangedError(ex);
						}
					}
				);
			}
		}

		protected HidPlusPlusDevice.FeatureAccess Device => Unsafe.As<HidPlusPlusDevice.FeatureAccess>(_device);

		protected bool HasBattery => Device.HasBatteryInformation;

		protected bool HasBacklight => Device.HasBacklight;

		protected bool HasLockKeys => Device.HasLockKeys;

		private event Action<Driver, BatteryState>? BatteryStateChanged;
		private event Action<Driver, BacklightState>? BacklightStateChanged;
		private event Action<Driver, LockKeys>? LockKeysChanged;

		event Action<Driver, BatteryState> IBatteryStateDeviceFeature.BatteryStateChanged
		{
			add => BatteryStateChanged += value;
			remove => BatteryStateChanged -= value;
		}

		BatteryState IBatteryStateDeviceFeature.BatteryState => BuildBatteryState(Device.BatteryPowerState);

		event Action<Driver, BacklightState> IKeyboardBacklightFeature.BacklightStateChanged
		{
			add => BacklightStateChanged += value;
			remove => BacklightStateChanged -= value;
		}

		BacklightState IKeyboardBacklightFeature.BacklightState => BuildBacklightState(Device.BacklightState);

		event Action<Driver, LockKeys> IKeyboardLockKeysFeature.LockedKeysChanged
		{
			add => LockKeysChanged += value;
			remove => LockKeysChanged -= value;
		}

		LockKeys IKeyboardLockKeysFeature.LockedKeys => (LockKeys)(byte)Device.LockKeys;
	}

	private abstract class RegisterAccessDirect : RegisterAccess
	{
		public RegisterAccessDirect(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirect> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	private abstract class RegisterAccessThroughReceiver : RegisterAccess
	{
		public RegisterAccessThroughReceiver(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	private abstract class FeatureAccessDirect : FeatureAccess
	{
		public FeatureAccessDirect(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirect> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	private abstract class FeatureAccessThroughReceiver : FeatureAccess
	{
		public FeatureAccessThroughReceiver(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
	}

	// Non abstract driver types that can be returned for each device kind.
	// Lot of dirty inheritance stuff here. Maybe some of it can be avoided, I'm not sure yet.
	private static class BaseDrivers
	{
		internal class RegisterAccessDirectGeneric : RegisterAccessDirect
		{
			public RegisterAccessDirectGeneric(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class RegisterAccessDirectKeyboard : RegisterAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			public RegisterAccessDirectKeyboard(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureSet.Empty<IKeyboardDeviceFeature>();
		}

		internal class RegisterAccessDirectMouse : RegisterAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			public RegisterAccessDirectMouse(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}

		internal class RegisterAccessThroughReceiverGeneric : RegisterAccessThroughReceiver
		{
			public RegisterAccessThroughReceiverGeneric(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class RegisterAccessThroughReceiverKeyboard : RegisterAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			public RegisterAccessThroughReceiverKeyboard(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureSet.Empty<IKeyboardDeviceFeature>();
		}

		internal class RegisterAccessThroughReceiverMouse : RegisterAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}

		internal class FeatureAccessDirectGeneric : FeatureAccessDirect
		{
			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal class FeatureAccessDirectKeyboard : FeatureAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IKeyboardDeviceFeature> _keyboardFeatures;

			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureSet.Empty<IKeyboardDeviceFeature>();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
		}

		internal class FeatureAccessDirectMouse : FeatureAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}

		internal sealed class FeatureAccessThroughReceiverGeneric : FeatureAccessThroughReceiver
		{
			public FeatureAccessThroughReceiverGeneric(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
		}

		internal sealed class FeatureAccessThroughReceiverKeyboard : FeatureAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureSet<IKeyboardDeviceFeature> _keyboardFeatures;

			public FeatureAccessThroughReceiverKeyboard(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureSet.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureSet.Empty<IKeyboardDeviceFeature>();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureSet<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverMouse : FeatureAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			public FeatureAccessThroughReceiverMouse(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureSet<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureSet.Empty<IMouseDeviceFeature>();
		}
	}

	// Root drivers, all implementing ISystemDeviceDriver.
	private static class SystemDrivers
	{
		internal class Receiver : RegisterAccess
		{
			private readonly IDriverRegistry _driverRegistry;
			private readonly ILoggerFactory _loggerFactory;
			private readonly Dictionary<HidPlusPlusDevice, LogitechUniversalDriver?> _children;
			private readonly AsyncLock _lock;

			public Receiver
			(
				HidPlusPlusDevice.RegisterAccessReceiver device,
				ILoggerFactory loggerFactory,
				ILogger<Receiver> logger,
				DeviceConfigurationKey configurationKey,
				ushort versionNumber,
				IDriverRegistry driverRegistry
			)
				: base(device, logger, configurationKey, versionNumber)
			{
				_driverRegistry = driverRegistry;
				_children = new();
				_lock = new();
				_loggerFactory = loggerFactory;
				device.DeviceDiscovered += OnChildDeviceDiscovered;
				device.DeviceConnected += OnChildDeviceConnected;
				device.DeviceDisconnected += OnChildDeviceDisconnected;
			}

			private HidPlusPlusDevice.RegisterAccessReceiver Device => Unsafe.As<HidPlusPlusDevice.RegisterAccessReceiver>(_device);

			public Task StartWatchingDevicesAsync(CancellationToken cancellationToken)
				=> Device.StartWatchingDevicesAsync(cancellationToken);

			private async void OnChildDeviceDiscovered(HidPlusPlusDevice receiver, HidPlusPlusDevice device)
			{
				try
				{
					using (await _lock.WaitAsync(default).ConfigureAwait(false))
					{
						_children.Add(device, null);
					}
				}
				catch
				{
					// TODO: Log
				}
			}

			private async void OnChildDeviceConnected(HidPlusPlusDevice receiver, HidPlusPlusDevice device)
			{
				try
				{
					using (await _lock.WaitAsync(default).ConfigureAwait(false))
					{
						var driver = CreateChildDriver(ConfigurationKey.DeviceMainId, device, _loggerFactory);

						_children[device] = driver;

						await _driverRegistry.AddDriverAsync(driver).ConfigureAwait(false);
					}
				}
				catch
				{
					// TODO: Log
				}
			}

			private async void OnChildDeviceDisconnected(HidPlusPlusDevice receiver, HidPlusPlusDevice device)
			{
				try
				{
					using (await _lock.WaitAsync(default).ConfigureAwait(false))
					{
						if (_children.TryGetValue(device, out var driver) && driver is not null)
						{
							_children[device] = null;

							await _driverRegistry.RemoveDriverAsync(driver).ConfigureAwait(false);
						}
					}
				}
				catch
				{
					// TODO: Log
				}
			}

			public override async ValueTask DisposeAsync()
			{
				await base.DisposeAsync().ConfigureAwait(false);
				_driverRegistry.Dispose();
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;
		}

		internal class UnifyingReceiver : Receiver
		{
			public UnifyingReceiver(HidPlusPlusDevice.UnifyingReceiver device, ILoggerFactory loggerFactory, ILogger<UnifyingReceiver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, IDriverRegistry driverRegistry)
				: base(device, loggerFactory, logger, configurationKey, versionNumber, driverRegistry)
			{
			}
		}

		internal class BoltReceiver : Receiver
		{
			public BoltReceiver(HidPlusPlusDevice.BoltReceiver device, ILoggerFactory loggerFactory, ILogger<BoltReceiver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, IDriverRegistry driverRegistry)
				: base(device, loggerFactory, logger, configurationKey, versionNumber, driverRegistry)
			{
			}
		}

		internal class RegisterAccessDirectGeneric : BaseDrivers.RegisterAccessDirectGeneric
		{
			public RegisterAccessDirectGeneric(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
			}
		}

		internal class RegisterAccessDirectKeyboard : BaseDrivers.RegisterAccessDirectKeyboard
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessDirectKeyboard(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class RegisterAccessDirectMouse : BaseDrivers.RegisterAccessDirectMouse
		{
			public RegisterAccessDirectMouse(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class RegisterAccessThroughReceiverGeneric : BaseDrivers.RegisterAccessThroughReceiverGeneric
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessThroughReceiverGeneric(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
			}
		}

		internal class RegisterAccessThroughReceiverKeyboard : BaseDrivers.RegisterAccessThroughReceiverKeyboard
		{
			public RegisterAccessThroughReceiverKeyboard(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class RegisterAccessThroughReceiverMouse : BaseDrivers.RegisterAccessThroughReceiverMouse
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class FeatureAccessDirectGeneric : BaseDrivers.FeatureAccessDirectGeneric
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
			}
		}

		internal class FeatureAccessDirectKeyboard : BaseDrivers.FeatureAccessDirectKeyboard
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class FeatureAccessDirectMouse : BaseDrivers.FeatureAccessDirectMouse
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}
	}
}
