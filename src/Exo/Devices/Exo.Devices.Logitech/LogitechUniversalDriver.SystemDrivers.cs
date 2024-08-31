using System.Runtime.CompilerServices;
using DeviceTools.Logitech.HidPlusPlus;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Logitech;

public abstract partial class LogitechUniversalDriver
{
	// Root drivers
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
			public RegisterAccessThroughReceiverMouse(HidPlusPlusDevice.RegisterAccessThroughReceiver device, ILogger<RegisterAccessThroughReceiverMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class FeatureAccessDirectGeneric : BaseDrivers.FeatureAccessDirectGeneric
		{
			public FeatureAccessDirectGeneric(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectGeneric> logger, DeviceConfigurationKey configurationKey, ushort versionNumber, DeviceCategory category)
				: base(device, logger, configurationKey, versionNumber, category)
			{
			}
		}

		internal class FeatureAccessDirectKeyboard : BaseDrivers.FeatureAccessDirectKeyboard
		{
			public FeatureAccessDirectKeyboard(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectKeyboard> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}

		internal class FeatureAccessDirectMouse : BaseDrivers.FeatureAccessDirectMouse
		{
			public FeatureAccessDirectMouse(HidPlusPlusDevice.FeatureAccessDirect device, ILogger<FeatureAccessDirectMouse> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
				: base(device, logger, configurationKey, versionNumber)
			{
			}
		}
	}
}
