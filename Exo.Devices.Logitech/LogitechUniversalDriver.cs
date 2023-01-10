using DeviceTools.HumanInterfaceDevices;
using Exo.Core;
using Exo.Core.Features;

namespace Exo.Devices.Logitech;

[DeviceId(VendorIdSource.Usb, 0x046D, 0xB361, "MX Keys for Mac")]
[DeviceId(VendorIdSource.Usb, 0x046D, 0xB36A, "MX Keys Mini for Mac")]
public class LogitechUniversalDriver : Driver, IDeviceDriver<IKeyboardDeviceFeature>
{
	public override IDeviceFeatureCollection<IDeviceFeature> Features => throw new NotImplementedException();

	IDeviceFeatureCollection<IKeyboardDeviceFeature> IDeviceDriver<IKeyboardDeviceFeature>.Features { get; }

	public override string FriendlyName { get; }

	protected override string GetConfigurationKey() => throw new NotImplementedException();

	public static ValueTask<LogitechUniversalDriver> CreateAsync(string path) => throw new NotImplementedException();
}
