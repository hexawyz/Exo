using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.DisplayDevices;
using Exo.I2C;

namespace Exo.Service;

/// <summary>Provides a fallback for monitor I2C buses by communicating with the UI-enabled helper.</summary>
/// <remarks>
/// <para>
/// This tries to decode I2C requests into higher-level DDC requests that will be executed through the DXVA2 api by the UI mode helper.
/// These DXVA2 APIs can only be used in interactive mode, hence the need for a helper process.
/// This is why we prefer having GPU drivers to access monitors. However, such drivers may not always be available.
/// </para>
/// <para>
/// Because of the limitations of the DXVA2 API, this I2C implementation only supports a limited set of features.
/// It only accepts requests to I2C address 0x37 and register 0x51.
/// The only DDC commands supported are capabilities, VCP Get and VCP Set.
/// </para>
/// </remarks>
internal sealed class ProxiedI2cBusProvider : II2cBusProvider
{
	internal sealed class AdapterMonitorResolver
	{
		private readonly IMonitorControlAdapter _adapter;
		private readonly Dictionary<(ushort, ushort, uint, string?), I2cBus> _knownMonitors;
		private readonly AsyncLock _lock;

		public AdapterMonitorResolver(IMonitorControlAdapter adapter)
		{
			_adapter = adapter;
			_knownMonitors = new();
			_lock = new();
		}

		public async ValueTask<II2cBus> ResolveI2cBus(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken)
		{
			var key = (vendorId.Value, productId, idSerialNumber, serialNumber);
			using (await _lock.WaitAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!_knownMonitors.TryGetValue(key, out var bus))
				{
					bus = new I2cBus(this, await _adapter.ResolveMonitorAsync(vendorId.Value, productId, idSerialNumber, serialNumber, cancellationToken).ConfigureAwait(false));
					_knownMonitors.Add(key, bus);
				}
				return bus;
			}
		}
	}

	internal sealed class I2cBus : II2cBus
	{
		private readonly IMonitorControlMonitor _monitor;
		private ImmutableArray<byte> _rawCapabilities;
		private VcpFeatureResponse _vcpResponse;
		private DdcCiCommand _lastRequest;
		private byte _lastVcpCode;
		private ushort _lastRequestOffset;
		private readonly AdapterMonitorResolver _adapterResolver;

		public I2cBus(AdapterMonitorResolver adapterResolver, IMonitorControlMonitor monitor)
		{
			_adapterResolver = adapterResolver;
			_monitor = monitor;
			_lastRequestOffset = 0xFFFF;
		}

		public ValueTask DisposeAsync() => _monitor.DisposeAsync();

		public ValueTask WriteAsync(byte address, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
			=> WriteAsync(address, bytes.Span[0], bytes[1..], cancellationToken);

		public async ValueTask WriteAsync(byte address, byte register, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
		{
			if (address != DisplayDataChannel.DefaultDeviceAddress) throw new ArgumentOutOfRangeException(nameof(address));
			if (register != 0x51) throw new ArgumentOutOfRangeException(nameof(register));
			if (Checksum.Xor(bytes.Span, (byte)((DisplayDataChannel.DefaultDeviceAddress << 1) ^ register)) != 0) throw new ArgumentException();
			byte dataLength = (byte)(bytes.Length - 2);
			if (bytes.Length > 0x7F || bytes.Span[0] != (dataLength | 0x80)) throw new ArgumentException("Invalid data length.");

			_lastRequest = (DdcCiCommand)bytes.Span[1];
			switch ((_lastRequest, dataLength))
			{
			case (DdcCiCommand.VcpRequest, 2):
				_vcpResponse = await _monitor.GetVcpFeatureAsync(_lastVcpCode = bytes.Span[2], cancellationToken);
				break;
			case (DdcCiCommand.VcpSet, 4):
				await _monitor.SetVcpFeatureAsync(bytes.Span[2], BigEndian.ReadUInt16(in bytes.Span[3]), cancellationToken);
				break;
			case (DdcCiCommand.CapabilitiesRequest, 3):
				if (_rawCapabilities.IsDefault)
				{
					_rawCapabilities = await _monitor.GetCapabilitiesAsync(cancellationToken);
				}
				ushort offset = BigEndian.ReadUInt16(in bytes.Span[2]);
				// Implements the simplified logic proposed in the DDC/CI standard, where we expect the requests to always be deterministic and otherwise resets to the start.
				_lastRequestOffset = _lastRequestOffset == 0xFFFF ||
					offset > _rawCapabilities.Length ||
					offset != _lastRequestOffset && offset != _lastRequestOffset + 32 && (_rawCapabilities.Length - _lastRequestOffset >= 32 || offset != _rawCapabilities.Length) ?
					(ushort)0 :
					offset;
				break;
			default:
				throw new InvalidOperationException("Unsupported operation.");
			}
		}

		public ValueTask ReadAsync(byte address, Memory<byte> bytes, CancellationToken cancellationToken)
			=> ReadAsync(address, 0x51, bytes, cancellationToken);

		public ValueTask ReadAsync(byte address, byte register, Memory<byte> bytes, CancellationToken cancellationToken)
		{
			try
			{
				if (address != DisplayDataChannel.DefaultDeviceAddress) throw new ArgumentOutOfRangeException(nameof(address));
				if (register != 0x51) throw new ArgumentOutOfRangeException(nameof(register));

				switch (_lastRequest)
				{
				case DdcCiCommand.VcpRequest:
					WriteVcpReply(bytes.Span);
					break;
				case DdcCiCommand.CapabilitiesRequest:
					WriteCapabilitiesReply(bytes.Span);
					break;
				default:
					throw new InvalidOperationException("Unsupported operation.");
				}
			}
			catch (Exception ex)
			{
				return ValueTask.FromException(ex);
			}
			return ValueTask.CompletedTask;
		}

		private void WriteVcpReply(Span<byte> buffer)
		{
			if (buffer.Length < 11) WriteVcpReplyWithIntermediateBuffer(buffer);
			else WriteVcpReplyInline(buffer);
		}

		private void WriteVcpReplyWithIntermediateBuffer(Span<byte> buffer)
		{
			Span<byte> intermediateBuffer = new byte[11];
			WriteVcpReplyInline(intermediateBuffer);
			intermediateBuffer[..buffer.Length].CopyTo(buffer);
		}

		private void WriteVcpReplyInline(Span<byte> buffer)
		{
			buffer[0] = DisplayDataChannel.DefaultDeviceAddress << 1;
			buffer[1] = 0x88;
			buffer[2] = (byte)DdcCiCommand.VcpReply;
			buffer[3] = 0;
			buffer[4] = _lastVcpCode;
			buffer[5] = _vcpResponse.IsMomentary ? (byte)1 : (byte)0;
			BigEndian.Write(ref buffer[6], _vcpResponse.MaximumValue);
			BigEndian.Write(ref buffer[8], _vcpResponse.CurrentValue);
			buffer[10] = Checksum.Xor(buffer[..10], 0x50);
		}

		private void WriteCapabilitiesReply(Span<byte> buffer)
		{
			int remainingBytes = _rawCapabilities.Length - _lastRequestOffset;
			int fragmentSize = Math.Min(remainingBytes, 32);
			int dataLength = fragmentSize + 6;
			if (buffer.Length < dataLength) WriteCapabilitiesReplyWithIntermediateBuffer(buffer, fragmentSize);
			else WriteCapabilitiesReplyInline(buffer, _lastRequestOffset, fragmentSize);
		}

		private void WriteCapabilitiesReplyWithIntermediateBuffer(Span<byte> buffer, int fragmentSize)
		{
			Span<byte> intermediateBuffer = new byte[fragmentSize + 6];
			WriteCapabilitiesReplyInline(intermediateBuffer, _lastRequestOffset, fragmentSize);
			intermediateBuffer[..buffer.Length].CopyTo(buffer);
		}

		private void WriteCapabilitiesReplyInline(Span<byte> buffer, ushort offset, int fragmentSize)
		{
			buffer[0] = DisplayDataChannel.DefaultDeviceAddress << 1;
			buffer[1] = (byte)(0x83 + fragmentSize);
			buffer[2] = (byte)DdcCiCommand.CapabilitiesReply;
			BigEndian.Write(ref buffer[3], offset);
			_rawCapabilities.AsSpan(offset, fragmentSize).CopyTo(buffer[5..]);
			int checksumIndex = fragmentSize + 5;
			buffer[checksumIndex] = Checksum.Xor(buffer[..checksumIndex], 0x50);
		}
	}

	private readonly IMonitorControlService _monitorControlService;

	public ProxiedI2cBusProvider(IMonitorControlService monitorControlService)
		=> _monitorControlService = monitorControlService;

	public async ValueTask<MonitorI2cBusResolver> GetMonitorBusResolverAsync(string deviceName, CancellationToken cancellationToken)
	{
		return new AdapterMonitorResolver(await _monitorControlService.ResolveAdapterAsync(deviceName, cancellationToken).ConfigureAwait(false)).ResolveI2cBus;
	}
}
