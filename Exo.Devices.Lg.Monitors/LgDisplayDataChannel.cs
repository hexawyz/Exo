using Exo.I2C;

namespace Exo.Devices.Lg.Monitors;

public class LgDisplayDataChannel : DisplayDataChannel
{
	public LgDisplayDataChannel(II2CBus? i2cBus, bool isOwned)
		: base(i2cBus, 104, isOwned)
	{
	}

	public async Task SendLgCustomCommandAsync(byte code, ushort value, Memory<byte> destination, CancellationToken cancellationToken)
	{
		var buffer = Buffer;
		var i2cBus = I2CBus;

		WriteVcpSet(buffer.Span, code, value, 0x6E ^ 0x50);

		await i2cBus.WriteAsync(0x6E, 0x50, buffer[..6], cancellationToken).ConfigureAwait(false);
		await Task.Delay(40, cancellationToken);
		await i2cBus.ReadAsync(0x6F, destination, cancellationToken).ConfigureAwait(false);
	}
}
