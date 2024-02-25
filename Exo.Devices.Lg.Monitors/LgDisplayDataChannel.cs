using Exo.I2C;

namespace Exo.Devices.Lg.Monitors;

public class LgDisplayDataChannel : DisplayDataChannel
{
	public LgDisplayDataChannel(II2CBus? i2cBus, bool isOwned)
		: base(i2cBus, 104, isOwned)
	{
	}

	public async Task SetLgCustomAsync(byte code, ushort value, CancellationToken cancellationToken)
	{
		var buffer = Buffer;
		var i2cBus = I2CBus;

		buffer.Span[0] = 0x50;
		WriteVcpSet(buffer.Span[1..], code, value, 0x6E ^ 0x50);

		await i2cBus.WriteAsync(0x6E, buffer[..7], cancellationToken).ConfigureAwait(false);
		await Task.Delay(50, cancellationToken);
	}

	public async Task SetLgCustomAsync(byte code, ushort value, Memory<byte> destination, CancellationToken cancellationToken)
	{
		var buffer = Buffer;
		var i2cBus = I2CBus;

		buffer.Span[0] = 0x50;
		WriteVcpSet(buffer.Span[1..], code, value, 0x6E ^ 0x50);

		await i2cBus.WriteAsync(0x6E, buffer[..7], cancellationToken).ConfigureAwait(false);
		await Task.Delay(50, cancellationToken);
		await i2cBus.ReadAsync(0x6F, destination, cancellationToken).ConfigureAwait(false);
	}

	public async Task GetLgCustomAsync(byte code, Memory<byte> destination, CancellationToken cancellationToken)
	{
		var buffer = Buffer;
		var i2cBus = I2CBus;

		buffer.Span[0] = 0x50;
		WriteVcpRequest(buffer.Span[1..], code, 0x6E ^ 0x50);

		await i2cBus.WriteAsync(0x6E, buffer[..5], cancellationToken).ConfigureAwait(false);
		await Task.Delay(50, cancellationToken);
		await i2cBus.ReadAsync(0x6F, destination, cancellationToken).ConfigureAwait(false);
	}
}
