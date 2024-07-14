using Exo.I2C;

namespace Exo.Devices.Lg.Monitors;

internal sealed class CompositeI2cBus : II2cBus
{
	private II2cBus[]? _buses = [];
	private bool _hasUsbBus;

	public void SetUsbBus(HidI2CTransport bus)
	{
		var oldBuses = _buses ?? throw new ObjectDisposedException(nameof(II2cBus));

		if (_hasUsbBus) throw new InvalidOperationException("The USB I2C bus for the device was already assigned. This exception should never happen.");

		var newBuses = new II2cBus[oldBuses.Length + 1];

		newBuses[0] = bus;
		Array.Copy(oldBuses, 0, newBuses, 1, oldBuses.Length);

		Volatile.Write(ref _buses, newBuses);
		_hasUsbBus = true;
	}

	public void AddBus(II2cBus bus)
	{
		var oldBuses = _buses ?? throw new ObjectDisposedException(nameof(II2cBus));
		var newBuses = new II2cBus[oldBuses.Length + 1];

		Array.Copy(oldBuses, newBuses, oldBuses.Length);
		newBuses[oldBuses.Length] = bus;

		Volatile.Write(ref _buses, newBuses);
	}

	public void UnsetUsbBus(HidI2CTransport bus)
	{
		var oldBuses = _buses ?? throw new ObjectDisposedException(nameof(II2cBus));

		if (!_hasUsbBus || oldBuses.Length == 0 || oldBuses[0] != bus) throw new InvalidOperationException("The USB I2C bus for the device was not assigned. This exception should never happen.");

		var newBuses = oldBuses.Length == 1 ? [] : new II2cBus[oldBuses.Length - 1];

		Array.Copy(oldBuses, 1, newBuses, 0, newBuses.Length);

		Volatile.Write(ref _buses, newBuses);
		_hasUsbBus = false;
	}

	public void RemoveBus(II2cBus bus)
	{
		var oldBuses = _buses ?? throw new ObjectDisposedException(nameof(II2cBus));

		int busIndex = Array.IndexOf(oldBuses, bus);

		if (busIndex <= 0) throw new InvalidOperationException("The specified bus for the device was not registered. This exception should never happen.");

		var newBuses = oldBuses.Length == 1 ? [] : new II2cBus[oldBuses.Length - 1];

		Array.Copy(oldBuses, newBuses, busIndex);
		Array.Copy(oldBuses, busIndex + 1, newBuses, busIndex, newBuses.Length - busIndex);

		Volatile.Write(ref _buses, newBuses);
		_hasUsbBus = false;
	}

	private II2cBus Bus
		=> Volatile.Read(ref _buses) is { Length: > 0 } buses ?
		buses[0] :
		throw new InvalidOperationException("There are no available I2C buses for the device. This exception should never happen.");

	public ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		=> Bus.WriteAsync(address, register, bytes, cancellationToken);

	public ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		=> Bus.WriteAsync(address, bytes, cancellationToken);

	public ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
		=> Bus.ReadAsync(address, bytes, cancellationToken);

	public ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
		=> Bus.ReadAsync(address, register, bytes, cancellationToken);

	public async ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _buses, null) is { } buses)
		{
			foreach (var bus in buses)
			{
				await bus.DisposeAsync();
			}
		}
	}
}
