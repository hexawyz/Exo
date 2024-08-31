using DeviceTools;
using Exo.Features;
using Exo.I2C;

namespace Exo.Service;

internal sealed class FallbackDisplayAdapterI2cBusProviderFeature : IDisplayAdapterI2cBusProviderFeature
{
	private readonly ProxiedI2cBusProvider _i2cBusProvider;
	private readonly string _adapterDeviceName;

	internal FallbackDisplayAdapterI2cBusProviderFeature(ProxiedI2cBusProvider i2cBusProvider, string adapterDeviceName)
	{
		_i2cBusProvider = i2cBusProvider;
		_adapterDeviceName = adapterDeviceName;
	}

	string IDisplayAdapterI2cBusProviderFeature.DeviceName => _adapterDeviceName;

	async ValueTask<II2cBus> IDisplayAdapterI2cBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
	{
		var resolver = await _i2cBusProvider.GetMonitorBusResolverAsync(_adapterDeviceName, cancellationToken).ConfigureAwait(false);
		return await resolver(vendorId, productId, idSerialNumber, serialNumber, cancellationToken).ConfigureAwait(false);
	}
}
