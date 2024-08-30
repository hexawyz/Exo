using DeviceTools.Logitech.HidPlusPlus;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Logitech;

public abstract partial class LogitechUniversalDriver
{
	private abstract class RegisterAccess : LogitechUniversalDriver
	{
		public RegisterAccess(HidPlusPlusDevice.RegisterAccess device, ILogger<RegisterAccess> logger, DeviceConfigurationKey configurationKey, ushort versionNumber)
			: base(device, logger, configurationKey, versionNumber)
		{
		}
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
}
