using System.Collections.Immutable;
using DeviceTools;
using Exo.I2C;

namespace Exo.Service;

internal sealed class ReconnectingMonitorControlService : IMonitorControlService
{
	private sealed class ReconnectingAdapter : IMonitorControlAdapter
	{
		public static async Task<IMonitorControlAdapter> CreateAsync(IMonitorControlService monitorControlService, string deviceName, CancellationToken cancellationToken)
		{
			var adapter = new ReconnectingAdapter(monitorControlService, deviceName);
			await adapter.ReconnectAsync(null, cancellationToken).ConfigureAwait(false);
			return adapter;
		}

		private readonly IMonitorControlService _monitorControlService;
		private IMonitorControlAdapter? _adapter;
		private readonly string _deviceName;
		private readonly AsyncLock _lock;

		private ReconnectingAdapter(IMonitorControlService monitorControlService, string deviceName)
		{
			_monitorControlService = monitorControlService;
			_lock = new();
			_deviceName = deviceName;
		}

		// Internal helper that must only be called when disconnection is detected.
		private async Task<IMonitorControlAdapter?> ReconnectAsync(IMonitorControlAdapter? adapter, CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (adapter == (adapter = _adapter))
				{
					Volatile.Write(ref _adapter, null);
					Volatile.Write(ref _adapter, await _monitorControlService.ResolveAdapterAsync(_deviceName, cancellationToken).ConfigureAwait(false));
				}
			}

			return adapter;
		}

		public async Task<IMonitorControlMonitor> ResolveUnderlyingMonitorAsync(ushort vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		{
			Retry:;
				var adapter = Volatile.Read(ref _adapter);
				if (adapter is null) goto InitializeBus;
				try
				{
					return await adapter.ResolveMonitorAsync(vendorId, productId, idSerialNumber, serialNumber, cancellationToken).ConfigureAwait(false);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
			InitializeBus:;
				adapter = await ReconnectAsync(adapter, cancellationToken).ConfigureAwait(false);
				goto Retry;
		}

		Task<IMonitorControlMonitor> IMonitorControlAdapter.ResolveMonitorAsync(ushort vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
			=> ReconnectingMonitor.CreateAsync(this, PnpVendorId.FromRaw(vendorId), productId, idSerialNumber, serialNumber, cancellationToken);
	}

	private sealed class ReconnectingMonitor : IMonitorControlMonitor
	{
		public static async Task<IMonitorControlMonitor> CreateAsync
		(
			ReconnectingAdapter adapter,
			PnpVendorId vendorId,
			ushort productId,
			uint idSerialNumber,
			string? serialNumber,
			CancellationToken cancellationToken
		)
		{
			var monitor = new ReconnectingMonitor(adapter, vendorId, productId, idSerialNumber, serialNumber);
			await monitor.ReconnectAsync(monitor, cancellationToken).ConfigureAwait(false);
			return monitor;
		}

		private readonly ReconnectingAdapter _adapter;
		private IMonitorControlMonitor? _monitor;
		private readonly PnpVendorId _vendorId;
		private readonly ushort _productId;
		private readonly uint _idSerialNumber;
		private readonly string? _serialNumber;
		private readonly AsyncLock _lock;
		private readonly CancellationTokenSource _disposeCancellationTokenSource;

		private ReconnectingMonitor
		(
			ReconnectingAdapter adapter,
			PnpVendorId vendorId,
			ushort productId,
			uint idSerialNumber,
			string? serialNumber
		)
		{
			_adapter = adapter;
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
				if (Interlocked.Exchange(ref _monitor, null) is { } i2cBus)
				{
					await i2cBus.DisposeAsync().ConfigureAwait(false);
				}
			}
		}

		// Internal helper that must only be called when disconnection is detected.
		private async Task<IMonitorControlMonitor?> ReconnectAsync(IMonitorControlMonitor? monitor, CancellationToken cancellationToken)
		{
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (monitor == (monitor = _monitor))
				{
					Volatile.Write(ref _monitor, null);
					Volatile.Write(ref _monitor, await _adapter.ResolveUnderlyingMonitorAsync(_vendorId.Value, _productId, _idSerialNumber, _serialNumber, cancellationToken).ConfigureAwait(false));
				}
			}

			return monitor;
		}

		public async Task<ImmutableArray<byte>> GetCapabilitiesAsync(CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(_disposeCancellationTokenSource.IsCancellationRequested, typeof(ReconnectingMonitor));
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var monitor = Volatile.Read(ref _monitor);
				if (monitor is null) goto InitializeBus;
				try
				{
					return await monitor.GetCapabilitiesAsync(cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(Monitor).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
			InitializeBus:;
				monitor = await ReconnectAsync(monitor, cts.Token).ConfigureAwait(false);
				goto Retry;
			}
		}

		public async Task<VcpFeatureResponse> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(_disposeCancellationTokenSource.IsCancellationRequested, typeof(ReconnectingMonitor));
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var monitor = Volatile.Read(ref _monitor);
				if (monitor is null) goto InitializeBus;
				try
				{
					return await monitor.GetVcpFeatureAsync(vcpCode, cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(Monitor).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
			InitializeBus:;
				monitor = await ReconnectAsync(monitor, cts.Token).ConfigureAwait(false);
				goto Retry;
			}
		}

		public async Task SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken)
		{
			ObjectDisposedException.ThrowIf(_disposeCancellationTokenSource.IsCancellationRequested, typeof(ReconnectingMonitor));
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCancellationTokenSource.Token, cancellationToken))
			{
			Retry:;
				var monitor = Volatile.Read(ref _monitor);
				if (monitor is null) goto InitializeBus;
				try
				{
					await monitor.SetVcpFeatureAsync(vcpCode, value, cts.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (_disposeCancellationTokenSource.IsCancellationRequested)
				{
					throw new ObjectDisposedException(typeof(Monitor).FullName);
				}
				catch (ObjectDisposedException)
				{
					goto InitializeBus;
				}
				return;
			InitializeBus:;
				monitor = await ReconnectAsync(monitor, cts.Token).ConfigureAwait(false);
				goto Retry;
			}
		}
	}

	private readonly IMonitorControlService _monitorControlService;

	public ReconnectingMonitorControlService(IMonitorControlService monitorControlService) => _monitorControlService = monitorControlService;

	public Task<IMonitorControlAdapter> ResolveAdapterAsync(string deviceName, CancellationToken cancellationToken)
		=> ReconnectingAdapter.CreateAsync(_monitorControlService, deviceName, cancellationToken);
}
