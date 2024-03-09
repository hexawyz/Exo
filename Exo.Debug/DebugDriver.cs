using Exo.Discovery;

namespace Exo.Debug;

public sealed class DebugDriver : Driver
{
	[DiscoverySubsystem<DebugDiscoverySystem>]
	[DebugFactory]
	public static ValueTask<DriverCreationResult<DebugDeviceKey>?> CreateAsync(DebugDriver? driver)
	{
		if (driver is null) return ValueTask.FromResult<DriverCreationResult<DebugDeviceKey>?>(null);

		return new(new DriverCreationResult<DebugDeviceKey>([driver._deviceId], driver));
	}

	private readonly Guid _deviceId;

	internal DebugDriver(Guid deviceId, string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
		_deviceId = deviceId;
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Other;

	public override ValueTask DisposeAsync() => throw new NotImplementedException();
}
