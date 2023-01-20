using System.Collections.Immutable;

namespace Exo;

[DeviceInterfaceClass(DeviceInterfaceClass.Hid)]
public abstract class HidDriver : Driver, ISystemDeviceDriver
{
	protected HidDriver(ImmutableArray<string> deviceNames, string friendlyName, DeviceConfigurationKey configurationKey) : base(friendlyName, configurationKey)
	{
		DeviceNames = deviceNames;
	}

	public ImmutableArray<string> DeviceNames { get; }
}
