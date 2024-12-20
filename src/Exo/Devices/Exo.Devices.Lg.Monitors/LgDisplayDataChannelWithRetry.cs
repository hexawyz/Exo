using DeviceTools.DisplayDevices.Mccs;
using Exo.I2C;

namespace Exo.Devices.Lg.Monitors;

// TODO: As (some of) the retry logic has been moved to the base DDC implementation, see what to do with this class.
// It can probably be removed, and some of the LG specific logic moved to the base class.
// Need to check: if the retry conditions in base DDC class are enough.
public class LgDisplayDataChannelWithRetry : LgDisplayDataChannel
{
	public LgDisplayDataChannelWithRetry(II2cBus i2cBus, bool isOwned, ushort retryCount)
		: base(i2cBus, retryCount, 100, isOwned)
	{
	}

	public async ValueTask<ushort> GetCapabilitiesWithRetryAsync(Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = RetryCount;
		while (true)
		{
			try
			{
				return await GetCapabilitiesAsync(destination, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
				await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	public async ValueTask<VcpFeatureReply> GetVcpFeatureWithRetryAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		int retryCount = RetryCount;
		while (true)
		{
			try
			{
				return await GetVcpFeatureAsync(vcpCode, cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is not ArgumentException && retryCount > 0)
			{
				retryCount--;
				await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	public async Task GetLgCustomWithRetryAsync(byte code, Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = RetryCount;
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
				await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	public async Task SetLgCustomWithRetryAsync(byte code, ushort value, CancellationToken cancellationToken)
	{
		int retryCount = RetryCount;
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
				await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	public async Task SetLgCustomWithRetryAsync(byte code, ushort value, Memory<byte> destination, CancellationToken cancellationToken)
	{
		int retryCount = RetryCount;
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
				await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
