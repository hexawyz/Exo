using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools.Bluetooth;
using Exo.ColorFormats;
using Exo.Lighting.Effects;
using Microsoft.Win32.SafeHandles;

namespace Exo.Devices.Razer;

// This is an implementation for the BT-LE mode of the DeathAdder V2 Pro mouse.
// It might be adaptable with other devices, but that is yet unknown.
// Also, it is important that this code does not run in parallel with another app such as Razer Synapse.
internal sealed class RazerDeathAdderV2ProBluetoothProtocolTransport : IRazerProtocolTransport
{
	private interface IWaitState
	{
		void HandlePacket(ReadOnlySpan<byte> data);
		Task WaitAsync(CancellationToken cancellationToken);
	}

	private interface IWaitState<T> : IWaitState
	{
		Task IWaitState.WaitAsync(CancellationToken cancellationToken) => WaitAsync(cancellationToken);
		new Task<T> WaitAsync(CancellationToken cancellationToken);
	}

	private sealed class SimpleWaitState : TaskCompletionSource, IWaitState
	{
		private readonly byte _commandId;

		public SimpleWaitState(byte commandId) => _commandId = commandId;

		public byte CommandId => _commandId;

		public void HandlePacket(ReadOnlySpan<byte> data)
		{
			if (Task.IsCompleted) return;

			if (data.Length < 2)
			{
				TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidDataException("Buffer too small.")));
				return;
			}

			if (data[0] == CommandId)
			{
				if (data[1] == 0) TrySetResult();
				else TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidDataException("Unexpected result.")));
			}
		}

		public Task WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);
	}

	private abstract class WaitState<T> : TaskCompletionSource<T>, IWaitState<T>
	{
		// In this implementation, we copy all the data bits into the buffer before actualling the parser method.
		// While it prevents live-parsing of data, it has two advantages:
		// 1 - Makes parsing simpler as all data is provided at once.
		// 2 - Make sure that we consume all the data packets even if a part is not needed. (Also handling exceptions when parsing middle packets would be problematic)
		private readonly byte[] _buffer;
		private readonly byte _commandId;
		private byte _dataLength;
		private byte _offset;

		protected WaitState(byte[] buffer, byte commandId)
		{
			_buffer = buffer;
			_commandId = commandId;
		}

		public byte CommandId => _commandId;

		void IWaitState.HandlePacket(ReadOnlySpan<byte> data)
		{
			if (Task.IsCompleted) return;

			int startOffset = 0;
			if (_dataLength == 0)
			{
				if (data.Length < 2)
				{
					TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new InvalidDataException("Buffer too small.")));
					return;
				}
				if (data[0] == CommandId)
				{
					_dataLength = data[1];
					if (_dataLength != 0) return;
					startOffset = 2;
				}
				else
				{
					// Ignore non-matching packets
					return;
				}
			}
			int packetDataLength = Math.Min(_dataLength - _offset, data.Length - startOffset);
			data.Slice(startOffset, packetDataLength).CopyTo(_buffer.AsSpan(_offset, packetDataLength));
			_offset = (byte)(_offset + packetDataLength);
			if (_offset == _dataLength)
			{
				try
				{
					TrySetResult(ProcessData(_buffer.AsSpan(0, _dataLength)));
				}
				catch (Exception ex)
				{
					TrySetException(ex);
				}
			}
		}

		public Task<T> WaitAsync(CancellationToken cancellationToken) => Task.WaitAsync(cancellationToken);

		protected abstract T ProcessData(ReadOnlySpan<byte> data);
	}

	private sealed class StringWaitState : WaitState<string>
	{
		public StringWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override string ProcessData(ReadOnlySpan<byte> data)
			=> Encoding.UTF8.GetString(data.TrimEnd((byte)0));
	}

	private sealed class ByteWaitState : WaitState<byte>
	{
		public ByteWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override byte ProcessData(ReadOnlySpan<byte> data)
			=> data[0];
	}

	private sealed class UInt16WaitState : WaitState<ushort>
	{
		public UInt16WaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override ushort ProcessData(ReadOnlySpan<byte> data)
			=> LittleEndian.ReadUInt16(in data[0]);
	}

	private sealed class BooleanWaitState : WaitState<bool>
	{
		public BooleanWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override bool ProcessData(ReadOnlySpan<byte> data)
			=> data[0] != 0;
	}

	private sealed class LightingEffectWaitState : WaitState<ILightingEffect?>
	{
		public LightingEffectWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override ILightingEffect? ProcessData(ReadOnlySpan<byte> data) => RazerProtocolTransport.ParseEffect(data);
	}

	private sealed class DpiWaitState : WaitState<DotsPerInch>
	{
		public DpiWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override DotsPerInch ProcessData(ReadOnlySpan<byte> data) => RazerProtocolTransport.ParseDpi(data, false);
	}

	private sealed class DpiProfilesWaitState : WaitState<RazerMouseDpiProfileConfiguration>
	{
		public DpiProfilesWaitState(byte[] buffer, byte commandId) : base(buffer, commandId) { }

		protected override RazerMouseDpiProfileConfiguration ProcessData(ReadOnlySpan<byte> data) => RazerProtocolTransport.ParseDpiProfileConfiguration(data, false);
	}

	private const int MaximumWritePacketLength = 20;

	private static readonly Guid WriteCharacteristicUuid = new(0x52401524, 0xF97C, 0x7F90, 0x0E, 0x7F, 0x6C, 0x6F, 0x4E, 0x36, 0xDB, 0x1C);
	private static readonly Guid ReadCharacteristicUuid = new(0x52401525, 0xF97C, 0x7F90, 0x0E, 0x7F, 0x6C, 0x6F, 0x4E, 0x36, 0xDB, 0x1C);

	private readonly SafeFileHandle _serviceHandle;
	private readonly byte[] _readBuffer;
	private AsyncLock? _lock;
	private IWaitState? _waitState;
	private readonly BluetoothLeCharacteristicInformation _writeCharacteristic;
	private readonly BluetoothLeHandle _readCharacteristicAttributeHandle;
	private readonly int _operationTimeout;
	private readonly IDisposable _eventRegistration;

	public RazerDeathAdderV2ProBluetoothProtocolTransport(SafeFileHandle serviceHandle, int operationTimeout)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(operationTimeout, 10);
		_serviceHandle = serviceHandle;
		_readBuffer = new byte[256];
		_lock = new();
		var characteristics = BluetoothLeDevice.GetCharacteristics(serviceHandle);
		ref var writeCharacteristic = ref Unsafe.NullRef<BluetoothLeCharacteristicInformation>();
		ref var readCharacteristic = ref Unsafe.NullRef<BluetoothLeCharacteristicInformation>();
		for (int i = 0; i < characteristics.Length; i++)
		{
			ref var characteristic = ref ImmutableCollectionsMarshal.AsArray(characteristics)![i];
			if (characteristic.CharacteristicUuid.LongId == WriteCharacteristicUuid)
			{
				writeCharacteristic = ref characteristic;
			}
			if (characteristic.CharacteristicUuid.LongId == ReadCharacteristicUuid)
			{
				readCharacteristic = ref characteristic;
			}
		}
		if (Unsafe.IsNullRef(in writeCharacteristic) || Unsafe.IsNullRef(in readCharacteristic))
		{
			throw new InvalidOperationException("Expected GATT characteristics not found.");
		}
		_writeCharacteristic = writeCharacteristic;
		_readCharacteristicAttributeHandle = readCharacteristic.AttributeHandle;
		_operationTimeout = operationTimeout;
		_eventRegistration = BluetoothLeDevice.RegisterValueChangedEvent
		(
			_serviceHandle,
			in readCharacteristic,
			static (attributeHandle, data, state) => (state as RazerDeathAdderV2ProBluetoothProtocolTransport)?.HandleNotification(attributeHandle, data),
			this
		); 
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _lock, null) is not null)
		{
			_eventRegistration.Dispose();
			_serviceHandle.Dispose();
		}
	}

	private AsyncLock GetLock()
	{
		var @lock = Volatile.Read(ref _lock);
		ObjectDisposedException.ThrowIf(@lock is null, typeof(RazerDeathAdderV2ProBluetoothProtocolTransport));
		return @lock;
	}

	private void HandleNotification(BluetoothLeHandle attributeHandle, ReadOnlySpan<byte> data)
	{
		if (Volatile.Read(ref _waitState) is { } waitState)
		{
			waitState.HandlePacket(data);
		}
	}

	public async ValueTask<string> GetSerialNumberAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 83 00 00 00 01 83 00 00
			((uint*)buffer)[1] = 0x83;
			((uint*)buffer)[2] = 0x8301;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new StringWaitState(_readBuffer, 0x83);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	// TODO: Should find a way to make this return the information that we nneed. The value is currently hardcoded.
	public ValueTask<byte> GetDeviceInformationXxxxxAsync(CancellationToken cancellationToken) => ValueTask.FromResult<byte>(4);

	public async ValueTask<ILightingEffect?> GetSavedEffectAsync(byte flag, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persisted, byte flag)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 93 00 00 00 10 83 <persisted> <magic>
			((uint*)buffer)[1] = 0x93;
			*(ushort*)&((uint*)buffer)[2] = 0x8310;
			buffer[4 + 6] = persisted ? (byte)1 : (byte)0;
			buffer[4 + 7] = flag;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new LightingEffectWaitState(_readBuffer, 0x93);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, true, flag);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}


	public async Task SetEffectAsync(bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persist, RazerLightingEffect effect, byte colorCount, RgbColor color1, RgbColor color2)
		{
			// Here we prepare the two data packets before sending them.
			// This allows relying on a shared implementation of the effect serialization code.
			const byte EffectBufferLength = 10;

			byte* buffer = stackalloc byte[4 + 8 + 4 + EffectBufferLength];
			((uint*)buffer)[0] = 8;
			// 13 <length> 00 00 10 03 <persisted> 00
			((uint*)buffer)[1] = 0x_00_00_00_13;
			*(ushort*)&((uint*)buffer)[2] = 0x_03_10;
			buffer[4 + 6] = persist ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			uint length = (uint)RazerProtocolTransport.WriteEffect(new Span<byte>(&buffer[4 + 8 + 4], EffectBufferLength), effect, colorCount, color1, color2);
			if (length > EffectBufferLength) throw new InvalidOperationException("Unsupported effect length");
			buffer[4 + 1] = (byte)length;
			((uint*)buffer)[3] = length;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, &buffer[4 + 8]);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x13);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, persist, effect, colorCount, color1, color2);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	// TODO
	public ValueTask SetDynamicColorAsync(RgbColor color, CancellationToken cancellationToken) => throw new NotImplementedException();

	public async Task SetBrightnessAsync(bool persist, byte value, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persist, byte value)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 15 01 00 00 10 05 <persist> 00
			((uint*)buffer)[1] = 0x_00_00_01_15;
			*(ushort*)&((uint*)buffer)[2] = 0x_05_10;
			buffer[4 + 6] = persist ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			// The second packed contains a single byte with the brightness value.
			((uint*)buffer)[0] = 1;
			buffer[4] = value;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x15);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, persist, value);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async ValueTask<DotsPerInch> GetDpiAsync(bool persisted, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persisted)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 8b 00 00 00 0b 81 <persisted> 00
			((uint*)buffer)[1] = 0x_00_00_00_8b;
			*(ushort*)&((uint*)buffer)[2] = 0x_81_0b;
			buffer[4 + 6] = persisted ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new DpiWaitState(_readBuffer, 0x8b);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, persisted);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async Task SetDpiAsync(bool persist, DotsPerInch dpi, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persist, DotsPerInch dpi)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 0b 06 00 00 0b 01 <persisted> 00
			((uint*)buffer)[1] = 0x_00_00_06_0b;
			*(ushort*)&((uint*)buffer)[2] = 0x_01_0b;
			buffer[4 + 6] = persist ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			((uint*)buffer)[0] = 6;
			RazerProtocolTransport.WriteDpi(new Span<byte>(&buffer[4], 6), dpi, false);
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x0b);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, persist, dpi);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
					return;
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async ValueTask<RazerMouseDpiProfileConfiguration> GetDpiPresetsAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persisted)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 8f 00 00 00 0b 84 <persisted> 00
			((uint*)buffer)[1] = 0x_00_00_00_8f;
			*(ushort*)&((uint*)buffer)[2] = 0x_84_0b;
			buffer[4 + 6] = persisted ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new DpiProfilesWaitState(_readBuffer, 0x8f);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, true);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async Task SetDpiProfilesAsync(bool persist, RazerMouseDpiProfileConfiguration configuration, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, bool persist, RazerMouseDpiProfileConfiguration configuration)
		{
			// The length is hardcoded to a maximum length of 5 profiles for now. The maximum might need to be made configurable in the future.
			Span<byte> profilesBuffer = stackalloc byte[2 * 5 * 7];
			byte* buffer = stackalloc byte[4 + MaximumWritePacketLength];

			uint remainingLength = (uint)RazerProtocolTransport.WriteProfileConfiguration(profilesBuffer, false, configuration);

			((uint*)buffer)[0] = 8;
			// 0f <length> 00 00 0b 04 <persisted> 00
			((uint*)buffer)[1] = 0x_00_00_00_0f;
			buffer[4 + 1] = (byte)remainingLength;
			*(ushort*)&((uint*)buffer)[2] = 0x_04_0b;
			buffer[4 + 6] = persist ? (byte)1 : (byte)0;
			buffer[4 + 7] = 0;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			int offset = 0;
			while (remainingLength != 0)
			{
				uint packetLength = Math.Min(remainingLength, MaximumWritePacketLength);
				((uint*)buffer)[0] = packetLength;
				profilesBuffer.Slice(offset, (int)packetLength).CopyTo(new(&buffer[4], MaximumWritePacketLength));
				offset += (int)packetLength;
				remainingLength -= packetLength;
				BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			}
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x0f);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, true, configuration);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	// NB: I don't think there is a way to read or configure through Bluetooth because Bluetooth doesn't allow a variable polling rate.
	// As such, we return the frequency divider corresponding to 125 Hz, which should be what is used in Bluetooth mode.
	public ValueTask<byte> GetPollingFrequencyDivider(CancellationToken cancellationToken)
		=> new(8);

	public Task SetPollingFrequencyDivider(byte divider, CancellationToken cancellationToken)
		=> throw new NotSupportedException("Cannot set polling frequency on Bluetooth devices.");

	public async ValueTask<byte> GetBatteryLevelAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 85 00 00 00 05 81 00 01
			((uint*)buffer)[1] = 0x_00_00_00_85;
			((uint*)buffer)[2] = 0x_01_00_81_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new ByteWaitState(_readBuffer, 0x85);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async ValueTask<byte> GetLowPowerThresholdAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 87 00 00 00 05 82 00 01 (NB: Not sure about last parameter, could need to be 0 ?)
			((uint*)buffer)[1] = 0x_00_00_00_87;
			((uint*)buffer)[2] = 0x_01_00_82_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new ByteWaitState(_readBuffer, 0x87);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async Task SetLowPowerThresholdAsync(byte value, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, byte value)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 07 01 00 00 05 02 00 01
			((uint*)buffer)[1] = 0x_00_00_01_07;
			((uint*)buffer)[2] = 0x_01_00_02_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			((uint*)buffer)[0] = 1;
			buffer[4] = value;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x07);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, value);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
					return;
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async ValueTask<ushort> GetIdleTimerAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 85 00 00 00 05 84 00 00
			((uint*)buffer)[1] = 0x_00_00_00_85;
			((uint*)buffer)[2] = 0x_00_00_84_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new UInt16WaitState(_readBuffer, 0x85);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async Task SetIdleTimerAsync(ushort value, CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic, ushort value)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 05 02 00 00 05 04 00 00
			((uint*)buffer)[1] = 0x_00_00_02_05;
			((uint*)buffer)[2] = 0x_00_00_04_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
			((uint*)buffer)[0] = 2;
			LittleEndian.Write(ref buffer[4], value);
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new SimpleWaitState(0x05);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic, value);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
					return;
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public async ValueTask<bool> IsConnectedToExternalPowerAsync(CancellationToken cancellationToken)
	{
		static unsafe void WriteData(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation writeCharacteristic)
		{
			byte* buffer = stackalloc byte[4 + 8];
			((uint*)buffer)[0] = 8;
			// 85 00 00 00 05 85 00 00
			((uint*)buffer)[1] = 0x_00_00_00_85;
			((uint*)buffer)[2] = 0x_00_00_85_05;
			BluetoothLeDevice.UnsafeWrite(serviceHandle, in writeCharacteristic, buffer);
		}

		using (await GetLock().WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			var waitState = new BooleanWaitState(_readBuffer, 0x85);
			Volatile.Write(ref _waitState, waitState);
			try
			{
				WriteData(_serviceHandle, in _writeCharacteristic);
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					cts.CancelAfter(_operationTimeout);
					return await waitState.WaitAsync(cts.Token).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException ex)
			{
				waitState.TrySetCanceled(ex.CancellationToken);
				throw;
			}
			finally
			{
				Volatile.Write(ref _waitState, null);
			}
		}
	}

	public ValueTask<bool> HandshakeAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

	ValueTask<PairedDeviceInformation> IRazerProtocolTransport.GetDeviceInformationAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
	ValueTask<PairedDeviceInformation[]> IRazerProtocolTransport.GetDevicePairingInformationAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

	ValueTask<ILightingEffect?> IRazerProtocolTransport.GetSavedLegacyEffectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
	Task IRazerProtocolTransport.SetLegacyEffectAsync(RazerLegacyLightingEffect effect, byte parameter, RgbColor color1, RgbColor color2, CancellationToken cancellationToken) => throw new NotSupportedException();
	Task IRazerProtocolTransport.SetLegacyBrightnessAsync(byte value, CancellationToken cancellationToken) => throw new NotSupportedException();
	ValueTask<byte> IRazerProtocolTransport.GetLegacyBrightnessAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
