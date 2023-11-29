using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.Logitech.HidPlusPlus;
using Exo.Features;
using Exo.Features.KeyboardFeatures;
using Microsoft.Extensions.Logging;
using static DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.Features.BacklightV2;
using BacklightState = Exo.Features.KeyboardFeatures.BacklightState;
using FeatureAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.FeatureAccessProtocol.DeviceType;
using RegisterAccessDeviceType = DeviceTools.Logitech.HidPlusPlus.RegisterAccessProtocol.DeviceType;

namespace Exo.Devices.Logitech;

// This driver is a catch-all for Logitech devices. On first approximation, they should all implement the proprietary HID++ protocol.
[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
[VendorId(VendorIdSource.Usb, LogitechUsbVendorId)]
public abstract class LogitechUniversalDriver : Driver,
	ISerialNumberDeviceFeature,
	IDeviceIdFeature
{
	private const int LogitechUsbVendorId = 0x046D;
	private const string LogitechDriverKey = "logi";

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
		//Properties.System.Devices.DeviceInstanceId,
		Properties.System.Devices.Parent,
		Properties.System.Devices.BusTypeGuid,
		Properties.System.Devices.ClassGuid,
		Properties.System.Devices.EnumeratorName,
	};

	public static async Task<LogitechUniversalDriver> CreateAsync
	(
		string deviceName,
		ushort productId,
		ushort version,
		Optional<IDriverRegistry> driverRegistry,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken
	)
	{
		// Need to find a way to exclude those better, but it will probably be more gracefully solved with the future device discovery and registration model.
		if (productId == 0xC231) throw new NotSupportedException("Logitech Hub Virtual Mouse is not supported.");
		if (productId == 0xC232) throw new NotSupportedException("Logitech Hub Virtual Keyboard is not supported.");

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
			throw new InvalidOperationException("No device interfaces compatible with logi HID++ found.");
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

		var devicesById = devices.ToDictionary(d => d.Id);

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
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
			deviceNames[i] = deviceInterface.Id;

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.ProductId.Key, out ushort pid))
			{
				throw new InvalidOperationException($"No HID product ID associated with the device interface {deviceInterface.Id}.");
			}

			if (pid != productId)
			{
				throw new InvalidOperationException($"Inconsistent product ID for the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out string? deviceInstanceId))
			{
				throw new InvalidOperationException($"No device instance ID found for device interface {deviceInterface.Id}.");
			}

			// We must go from Device Interface to Device (Top Level Collection) to Device (USB/BT Interface) to Device (Parent)
			// Like most code here, we don't expect this to fail in normal conditions, so throwing an exception here is acceptable.
			var device = devicesById[deviceInstanceId];
			var parentDevice = devicesById[(string)device.Properties[Properties.System.Devices.Parent.Key]!];
			var topLevelDevice = devicesById[(string)parentDevice.Properties[Properties.System.Devices.Parent.Key]!];
			string topLevelDeviceName = topLevelDevice.Id;

			// We also verify that all device interfaces point towards the same top level parent. Otherwise, it would indicate that the logic should be reworked.
			// PS: I don't know, if there is a simple way to detect if a device node is a multi interface device node or not, hence the naïve lookup above where we assume a static structure.
			if (deviceNames[^1] is null)
			{
				deviceNames[^1] = topLevelDeviceName;

				// Try to find the appropriate device ID source based on the device enumerator.

				Guid guid;

				if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.BusTypeGuid.Key, out guid))
				{
					if (guid == DeviceBusTypesGuids.Usb)
					{
						deviceIdSource = DeviceIdSource.Usb;
						goto DeviceIdSourceResolved;
					}
				}

				if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.ClassGuid.Key, out guid))
				{
					if (guid == DeviceClassGuids.Usb)
					{
						deviceIdSource = DeviceIdSource.Usb;
						goto DeviceIdSourceResolved;
					}
					if (guid == DeviceClassGuids.Bluetooth)
					{
						if (topLevelDevice.Properties.TryGetValue(Properties.System.Devices.EnumeratorName.Key, out string? enumeratorName))
						{
							if (enumeratorName == "BTHLE")
							{
								deviceIdSource = DeviceIdSource.BluetoothLowEnergy;
								goto DeviceIdSourceResolved;
							}
							deviceIdSource = DeviceIdSource.Bluetooth;
							goto DeviceIdSourceResolved;
						}
					}
				}
			DeviceIdSourceResolved:;
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
					var collectionDescriptor = await hid.GetCollectionDescriptorAsync(cancellationToken).ConfigureAwait(false);
					if (!(collectionDescriptor.InputReportLength == 64 && collectionDescriptor.OutputReportLength == 64 && collectionDescriptor.FeatureReportLength == 0)) continue;
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
		var configurationKey = new DeviceConfigurationKey(LogitechDriverKey, deviceNames[^1], $"{LogitechUsbVendorId:X4}:{hppDevice.MainProductId:X4}", hppDevice.SerialNumber);

		var driver = CreateDriver(driverRegistry, loggerFactory, Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames), hppDevice, configurationKey, version);

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

		return driver;
	}

	private static LogitechUniversalDriver CreateDriver
	(
		Optional<IDriverRegistry> driverRegistry,
		ILoggerFactory loggerFactory,
		ImmutableArray<string> deviceNames,
		HidPlusPlusDevice hppDevice,
		DeviceConfigurationKey configurationKey,
		ushort versionNumber
	)
	{
		switch (hppDevice)
		{
		case HidPlusPlusDevice.UnifyingReceiver unifyingReceiver:
			return new SystemDrivers.UnifyingReceiver(unifyingReceiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.UnifyingReceiver>(), deviceNames, configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.BoltReceiver boltReceiver:
			return new SystemDrivers.BoltReceiver(boltReceiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.BoltReceiver>(), deviceNames, configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.RegisterAccessReceiver receiver:
			return new SystemDrivers.Receiver(receiver, loggerFactory, loggerFactory.CreateLogger<SystemDrivers.Receiver>(), deviceNames, configurationKey, versionNumber, driverRegistry.GetOrCreateValue());
		case HidPlusPlusDevice.RegisterAccessDirect rapDirect:
			driverRegistry.Dispose();
			return rapDirect.DeviceType switch
			{
				RegisterAccessDeviceType.Keyboard => new SystemDrivers.RegisterAccessDirectKeyboard(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectKeyboard>(), deviceNames, configurationKey, versionNumber),
				RegisterAccessDeviceType.Mouse => new SystemDrivers.RegisterAccessDirectMouse(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectMouse>(), deviceNames, configurationKey, versionNumber),
				_ => new SystemDrivers.RegisterAccessDirectGeneric(rapDirect, loggerFactory.CreateLogger<SystemDrivers.RegisterAccessDirectGeneric>(), deviceNames, configurationKey, versionNumber, GetDeviceCategory(rapDirect.DeviceType)),
			};
		case HidPlusPlusDevice.FeatureAccessDirect fapDirect:
			driverRegistry.Dispose();
			return fapDirect.DeviceType switch
			{
				FeatureAccessDeviceType.Keyboard => new SystemDrivers.FeatureAccessDirectKeyboard(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectKeyboard>(), deviceNames, configurationKey, versionNumber),
				FeatureAccessDeviceType.Mouse => new SystemDrivers.FeatureAccessDirectMouse(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectMouse>(), deviceNames, configurationKey, versionNumber),
				_ => new SystemDrivers.FeatureAccessDirectGeneric(fapDirect, loggerFactory.CreateLogger<SystemDrivers.FeatureAccessDirectGeneric>(), deviceNames, configurationKey, versionNumber, GetDeviceCategory(fapDirect.DeviceType)),
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

	protected LogitechUniversalDriver(HidPlusPlusDevice device, ILogger<LogitechUniversalDriver> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
		: base(device.FriendlyName ?? InferDeviceName(device), configurationKey)
	{
		_device = device;
		_logger = logger;
		_versionNumber = versionNumber;
	}

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
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessDirectGeneric(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessDirectGeneric, IDeviceIdFeature>(this);
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class RegisterAccessDirectKeyboard : RegisterAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessDirectKeyboard(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessDirectKeyboard, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureCollection.Empty<IKeyboardDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class RegisterAccessDirectMouse : RegisterAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessDirectMouse(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessDirectMouse, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureCollection.Empty<IMouseDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class RegisterAccessThroughReceiverGeneric : RegisterAccessThroughReceiver
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessThroughReceiverGeneric(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessThroughReceiverGeneric, IDeviceIdFeature>(this);
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class RegisterAccessThroughReceiverKeyboard : RegisterAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessThroughReceiverKeyboard(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessThroughReceiverKeyboard, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => FeatureCollection.Empty<IKeyboardDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class RegisterAccessThroughReceiverMouse : RegisterAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = FeatureCollection.Create<IDeviceFeature, RegisterAccessThroughReceiverMouse, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureCollection.Empty<IMouseDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class FeatureAccessDirectGeneric : FeatureAccessDirect
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectGeneric, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectGeneric, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectGeneric, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectGeneric, IDeviceIdFeature>(this);
				DeviceCategory = category;
			}

			public override DeviceCategory DeviceCategory { get; }
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class FeatureAccessDirectKeyboard : FeatureAccessDirect, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IKeyboardDeviceFeature> _keyboardFeatures;
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessDirectKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureCollection.Empty<IKeyboardDeviceFeature>();

				var baseFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectKeyboard, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectKeyboard, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectKeyboard, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectKeyboard, IDeviceIdFeature>(this);

				_allFeatures = FeatureCollection.CreateMerged(_keyboardFeatures, baseFeatures);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal class FeatureAccessDirectMouse : FeatureAccessDirect, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectMouse, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectMouse, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectMouse, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessDirectMouse, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureCollection.Empty<IMouseDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverGeneric : FeatureAccessThroughReceiver
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessThroughReceiverGeneric(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceCategory = category;
				_allFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverGeneric, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverGeneric, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverGeneric, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverGeneric, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory { get; }
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverKeyboard : FeatureAccessThroughReceiver, IDeviceDriver<IKeyboardDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IKeyboardDeviceFeature> _keyboardFeatures;
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessThroughReceiverKeyboard(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_keyboardFeatures = HasLockKeys ?
					HasBacklight ?
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature, IKeyboardBacklightFeature>(this) :
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardLockKeysFeature>(this) :
					HasBacklight ?
						FeatureCollection.Create<IKeyboardDeviceFeature, FeatureAccessThroughReceiverKeyboard, IKeyboardBacklightFeature>(this) :
						FeatureCollection.Empty<IKeyboardDeviceFeature>();

				var baseFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverKeyboard, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverKeyboard, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverKeyboard, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverKeyboard, IDeviceIdFeature>(this);

				_allFeatures = FeatureCollection.CreateMerged(_keyboardFeatures, baseFeatures);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Keyboard;

			IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features => _keyboardFeatures;
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}

		internal sealed class FeatureAccessThroughReceiverMouse : FeatureAccessThroughReceiver, IDeviceDriver<IMouseDeviceFeature>
		{
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

			public FeatureAccessThroughReceiverMouse(HidPlusPlusDevice.FeatureAccessThroughReceiver device, ILogger<FeatureAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				_allFeatures = HasSerialNumber ?
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverMouse, IDeviceIdFeature, ISerialNumberDeviceFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverMouse, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					HasBattery ?
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverMouse, IDeviceIdFeature, IBatteryStateDeviceFeature>(this) :
						FeatureCollection.Create<IDeviceFeature, FeatureAccessThroughReceiverMouse, IDeviceIdFeature>(this);
			}

			public override DeviceCategory DeviceCategory => DeviceCategory.Mouse;

			IDeviceFeatureCollection<IMouseDeviceFeature> IDeviceDriver<IMouseDeviceFeature>.Features => FeatureCollection.Empty<IMouseDeviceFeature>();
			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;
		}
	}

	// Root drivers, all implementing ISystemDeviceDriver.
	private static class SystemDrivers
	{
		internal class Receiver : RegisterAccess, ISystemDeviceDriver
		{
			private readonly IDriverRegistry _driverRegistry;
			private readonly ILoggerFactory _loggerFactory;
			private readonly Dictionary<HidPlusPlusDevice, LogitechUniversalDriver?> _children;
			private readonly AsyncLock _lock;
			private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
			public ImmutableArray<string> DeviceNames { get; }

			public Receiver
			(
				HidPlusPlusDevice.RegisterAccessReceiver device,
				ILoggerFactory loggerFactory,
				ILogger<Receiver> logger,
				ImmutableArray<string> deviceNames,
				DeviceConfigurationKey configurationKey,
				ushort versionNumber,
				IDriverRegistry driverRegistry
			)
				: base(device, logger, configurationKey, versionNumber)
			{
				_driverRegistry = driverRegistry;
				_children = new();
				_lock = new();
				_allFeatures = HasSerialNumber ?
					FeatureCollection.Create<IDeviceFeature, Receiver, IDeviceIdFeature, ISerialNumberDeviceFeature>(this) :
					FeatureCollection.Create<IDeviceFeature, Receiver, IDeviceIdFeature>(this);
				_loggerFactory = loggerFactory;
				DeviceNames = deviceNames;
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
						var driver = CreateChildDriver(DeviceNames[^1], device, _loggerFactory);

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

			public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

			public override DeviceCategory DeviceCategory => DeviceCategory.UsbWirelessReceiver;
		}

		internal class UnifyingReceiver : Receiver
		{
			public UnifyingReceiver(HidPlusPlusDevice.UnifyingReceiver device, ILoggerFactory loggerFactory, ILogger<UnifyingReceiver> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber, IDriverRegistry driverRegistry)
				: base(device, loggerFactory, logger, deviceNames, configurationKey, versionNumber, driverRegistry)
			{
			}
		}

		internal class BoltReceiver : Receiver
		{
			public BoltReceiver(HidPlusPlusDevice.BoltReceiver device, ILoggerFactory loggerFactory, ILogger<BoltReceiver> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber, IDriverRegistry driverRegistry)
				: base(device, loggerFactory, logger, deviceNames, configurationKey, versionNumber, driverRegistry)
			{
			}
		}

		internal class RegisterAccessDirectGeneric : BaseDrivers.RegisterAccessDirectGeneric, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessDirectGeneric(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectGeneric> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class RegisterAccessDirectKeyboard : BaseDrivers.RegisterAccessDirectKeyboard, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessDirectKeyboard(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectKeyboard> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class RegisterAccessDirectMouse : BaseDrivers.RegisterAccessDirectMouse, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessDirectMouse(HidPlusPlusDevice.RegisterAccessDirect device, ILogger<RegisterAccessDirectMouse> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class RegisterAccessThroughReceiverGeneric : BaseDrivers.RegisterAccessThroughReceiverGeneric, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessThroughReceiverGeneric(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverGeneric> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class RegisterAccessThroughReceiverKeyboard : BaseDrivers.RegisterAccessThroughReceiverKeyboard, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessThroughReceiverKeyboard(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverKeyboard> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class RegisterAccessThroughReceiverMouse : BaseDrivers.RegisterAccessThroughReceiverMouse, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class FeatureAccessDirectGeneric : BaseDrivers.FeatureAccessDirectGeneric, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class FeatureAccessDirectKeyboard : BaseDrivers.FeatureAccessDirectKeyboard, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}

		internal class FeatureAccessDirectMouse : BaseDrivers.FeatureAccessDirectMouse, ISystemDeviceDriver
		{
			public ImmutableArray<string> DeviceNames { get; }

			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, ImmutableArray<string> deviceNames, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
				DeviceNames = deviceNames;
			}
		}
	}

	private bool HasSerialNumber => ConfigurationKey.UniqueId is { Length: not 0 };

	public string SerialNumber
		=> HasSerialNumber ?
			ConfigurationKey.UniqueId! :
			throw new NotSupportedException("This device does not support the Serial Number feature.");

	public DeviceId DeviceId => new(_device.MainDeviceId.Source, VendorIdSource.Usb, LogitechUsbVendorId, _device.MainDeviceId.ProductId, _versionNumber);
}
