using DeviceTools;

namespace Exo.Services;

/// <summary>Provides the raw API to interact with an I2C bus.</summary>
public interface II2CBus : IDisposable
{
	/// <summary>Writes data to the specified register.</summary>
	/// <remarks>
	/// The bytes sent to the I2C device are to be formatted properly by the caller, including the checksum.
	/// The device address is fully computed (it includes the read or write bit), and it will be emitted automatically, as well as the register, before the provided bytes.
	/// </remarks>
	/// <param name="address">The final register address as it will be emitted to the I2C device.</param>
	/// <param name="register">The register to write.</param>
	/// <param name="bytes">The raw message bytes, including the checksum that should be computed by the caller.</param>
	/// <returns></returns>
	ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes);

	//ValueTask WriteAsync(byte address, ushort register, ReadOnlySpan<byte> bytes);

	/// <summary>Reads data from the HID bus.</summary>
	/// <param name="address">The device address that is to be read from.</param>
	/// <param name="bytes">The buffer of bytes that will be filled with the response, including the checksum.</param>
	/// <returns></returns>
	ValueTask ReadAsync(byte address, ReadOnlyMemory<byte> bytes);
}

/// <summary>A resolver that will provide an I2C bus for the requested monitor.</summary>
/// <remarks>
/// Success of the operation is not guaranteed, as the monitor may not be found for various reasons.
/// Well-functioning systems on which devices are not being disconnected on the fly should generally not fail to find the I2C bus to use with a monitor, however.
/// </remarks>
/// <param name="vendorId">The vendor ID of the monitor.</param>
/// <param name="productId">The product ID of the monitor.</param>
/// <param name="idSerialNumber">The ID serial number of the monitor, if available.</param>
/// <param name="serialNumber">The actual serial number of the monitor, if available.</param>
/// <param name="cancellationToken"></param>
/// <returns></returns>
public delegate ValueTask<II2CBus> MonitorI2CBusResolver(PnpVendorId vendorId, ushort productId, int idSerialNumber, string? serialNumber, CancellationToken cancellationToken);

public interface II2CBusProvider
{
	ValueTask<MonitorI2CBusResolver> GetMonitorBusResolver(string deviceName, CancellationToken cancellationToken);
	//ValueTask<II2CBus> GetBus(string deviceName, CancellationToken cancellationToken);
}

public interface II2CBusRegistry
{
	/// <summary>Registers a I2C bus provider for a video adapter.</summary>
	/// <remarks>
	/// <para>
	/// Monitors use a I2C bus to implement DDC/CI.
	/// Each monitor connected to a display adapter has its own I2C bus that is separate from the others.
	/// </para>
	/// <para>
	/// Display adapters are identified by their unique device name, and the display adapter to which any monitor is attached should be well-known.
	/// This will allow resolving the 
	/// </para>
	/// </remarks>
	/// <param name="deviceName">The device name of the video adapter.</param>
	/// <param name="resolver"></param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	IDisposable RegisterBusResolver(string deviceName, MonitorI2CBusResolver resolver);
	//IDisposable RegisterBus(string deviceName, II2CBus bus);
}
