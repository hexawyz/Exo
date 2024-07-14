using DeviceTools;
using Exo.I2C;

namespace Exo.Features;

public interface IDisplayAdapterI2CBusProviderFeature : IDisplayAdapterDeviceFeature
{
	/// <summary>This should return the device name of the display adapter.</summary>
	/// <remarks>This information is necessary for monitors to find the proper I2C bus based on the name of the display adapter they are connected to.</remarks>
	string DeviceName { get; }

	ValueTask<II2cBus> GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken);
}
