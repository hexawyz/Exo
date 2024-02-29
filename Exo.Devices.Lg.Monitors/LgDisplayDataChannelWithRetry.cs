using Exo.I2C;

namespace Exo.Devices.Lg.Monitors;

public class LgDisplayDataChannelWithRetry : LgDisplayDataChannel
{
	private readonly int _retryCount;

	public LgDisplayDataChannelWithRetry(II2CBus i2cBus, bool isOwned, int retryCount)
		: base(i2cBus, isOwned)
	{
		_retryCount = retryCount;
	}

	public async ValueTask<ushort> GetCapabilitiesWithRetryAsync(Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = _retryCount;
		while (true)
		{
			try
			{
				return await GetCapabilitiesAsync(destination, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
			}
		}
	}

	public async ValueTask<VcpFeatureResponse> GetVcpFeatureWithRetryAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		int retryCount = _retryCount;
		while (true)
		{
			try
			{
				return await GetVcpFeatureAsync(vcpCode, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
			}
		}
	}

	public async Task GetLgCustomWithRetryAsync(byte code, Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = _retryCount;
		while (true)
		{
			try
			{
				await GetLgCustomAsync(code, destination, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
			}
		}
	}

	public async Task SetLgCustomWithRetryAsync(byte code, ushort value, CancellationToken cancellationToken)
	{
		int retryCount = _retryCount;
		while (true)
		{
			try
			{
				await SetLgCustomAsync(code, value, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
			}
		}
	}

	public async Task SetLgCustomWithRetryAsync(byte code, ushort value, Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = _retryCount;
		while (true)
		{
			try
			{
				await SetLgCustomAsync(code, value, destination, cancellationToken).ConfigureAwait(false);
				return;
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
			}
		}
	}
}
