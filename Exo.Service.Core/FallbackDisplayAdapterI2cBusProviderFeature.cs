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

	ValueTask<II2cBus> IDisplayAdapterI2cBusProviderFeature.GetBusForMonitorAsync(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		=> new(new ReconnectingI2cBus(_i2cBusProvider, _adapterDeviceName, vendorId, productId, idSerialNumber, serialNumber));

	private sealed class ReconnectingI2cBus : II2cBus
	{
		private II2cBus? _i2cBus;
		private readonly ProxiedI2cBusProvider _i2cBusProvider;
		private readonly string _adapterDeviceName;
		private readonly PnpVendorId _vendorId;
		private readonly ushort _productId;
		private readonly uint _idSerialNumber;
		private readonly string? _serialNumber;
		private readonly AsyncLock _lock;
		private readonly CancellationTokenSource _disposeCancellationTokenSource;

		public ReconnectingI2cBus
		(
			ProxiedI2cBusProvider i2cBusProvider,
			string adapterDeviceName,
			PnpVendorId vendorId,
			ushort productId,
			uint idSerialNumber,
			string? serialNumber
		)
		{
			_i2cBusProvider = i2cBusProvider;
			_adapterDeviceName = adapterDeviceName;
			_vendorId = vendorId;
			_productId = productId;
			_idSerialNumber = idSerialNumber;
			_serialNumber = serialNumber;
			_lock = new();
			_disposeCancellationTokenSource = new();
		}

		public async ValueTask DisposeAsync()
		{
			_disposeCancellationTokenSource.Cancel();
			using (await _lock.WaitAsync(default).ConfigureAwait(false))
			{
				if (Interlocked.Exchange(ref _i2cBus, null) is { } i2cBus)
				{
					await i2cBus.DisposeAsync().ConfigureAwait(false);
				}
			}
		}

		public async ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			if (_disposeCancellationTokenSource.IsCancellationRequested) throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName); 
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var i2cBus = _i2cBus;
				if (i2cBus is null) goto InitializeBus;
				try
				{
					await i2cBus.WriteAsync(address, register, bytes, cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
				return;
			InitializeBus:;
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					if (ReferenceEquals(_i2cBus, i2cBus))
					{
						_i2cBus = null;
						var resolver = await _i2cBusProvider.GetMonitorBusResolverAsync(_adapterDeviceName, cancellationToken).ConfigureAwait(false);
						i2cBus = await resolver(_vendorId, _productId, _idSerialNumber, _serialNumber, cancellationToken).ConfigureAwait(false);
						Volatile.Write(ref _i2cBus, i2cBus);
					}
				}
				goto Retry;
			}
		}

		public async ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			if (_disposeCancellationTokenSource.IsCancellationRequested) throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName); 
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var i2cBus = _i2cBus;
				if (i2cBus is null) goto InitializeBus;
				try
				{
					await i2cBus.WriteAsync(address, bytes, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
				return;
			InitializeBus:;
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					if (ReferenceEquals(_i2cBus, i2cBus))
					{
						_i2cBus = null;
						var resolver = await _i2cBusProvider.GetMonitorBusResolverAsync(_adapterDeviceName, cancellationToken).ConfigureAwait(false);
						i2cBus = await resolver(_vendorId, _productId, _idSerialNumber, _serialNumber, cancellationToken).ConfigureAwait(false);
						Volatile.Write(ref _i2cBus, i2cBus);
					}
				}
				goto Retry;
			}
		}

		public async ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			if (_disposeCancellationTokenSource.IsCancellationRequested) throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName); 
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var i2cBus = _i2cBus;
				if (i2cBus is null) goto InitializeBus;
				try
				{
					await i2cBus.ReadAsync(address, register, bytes, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
				return;
			InitializeBus:;
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					if (ReferenceEquals(_i2cBus, i2cBus))
					{
						_i2cBus = null;
						var resolver = await _i2cBusProvider.GetMonitorBusResolverAsync(_adapterDeviceName, cancellationToken).ConfigureAwait(false);
						i2cBus = await resolver(_vendorId, _productId, _idSerialNumber, _serialNumber, cancellationToken).ConfigureAwait(false);
						Volatile.Write(ref _i2cBus, i2cBus);
					}
				}
				goto Retry;
			}
		}

		public async ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			if (_disposeCancellationTokenSource.IsCancellationRequested) throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName); 
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var i2cBus = _i2cBus;
				if (i2cBus is null) goto InitializeBus;
				try
				{
					await i2cBus.ReadAsync(address, bytes, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(ReconnectingI2cBus).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
				return;
			InitializeBus:;
				using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
				{
					if (ReferenceEquals(_i2cBus, i2cBus))
					{
						_i2cBus = null;
						var resolver = await _i2cBusProvider.GetMonitorBusResolverAsync(_adapterDeviceName, cancellationToken).ConfigureAwait(false);
						i2cBus = await resolver(_vendorId, _productId, _idSerialNumber, _serialNumber, cancellationToken).ConfigureAwait(false);
						Volatile.Write(ref _i2cBus, i2cBus);
					}
				}
				goto Retry;
			}
		}
	}
}
