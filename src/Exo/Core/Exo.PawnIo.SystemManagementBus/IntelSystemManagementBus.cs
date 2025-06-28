using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Exo.Features.Motherboards;

namespace Exo.SystemManagementBus;

public sealed class IntelSystemManagementBus : ISystemManagementBus, IMotherboardSystemManagementBusFeature
{
	private const uint ErrorNoSuchDevice = 0x800701B1;

	private readonly PawnIo _pawnIo;

	public IntelSystemManagementBus()
	{
		var pawnIo = new PawnIo();
		try
		{
			pawnIo.LoadKnownModule(PawnIoKnownModule.SmbusI801);
		}
		catch
		{
			pawnIo.Dispose();
			throw;
		}
		_pawnIo = pawnIo;
	}

	ValueTask<OwnedMutex> ISystemManagementBus.AcquireMutexAsync() => new(AsyncGlobalMutex.SmBus.AcquireAsync());

	ValueTask ISystemManagementBus.QuickWriteAsync(byte address)
	{
		try
		{
			_pawnIo.Execute("ioctl_i801_write_quick\0"u8, [address, 0], []);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return ValueTask.CompletedTask;
	}

	ValueTask ISystemManagementBus.QuickReadAsync(byte address)
	{
		try
		{
			_pawnIo.Execute("ioctl_i801_write_quick\0"u8, [address, 1], []);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return ValueTask.CompletedTask;
	}

	ValueTask ISystemManagementBus.SendByteAsync(byte address, byte value)
	{
		try
		{
			_pawnIo.Execute("ioctl_i801_write_byte\0"u8, [address, value], []);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<byte> ISystemManagementBus.ReceiveByteAsync(byte address)
	{
		try
		{
			Span<ulong> buffer = stackalloc ulong[2];
			buffer[0] = address;
			_pawnIo.Execute("ioctl_i801_read_byte\0"u8, buffer[..1], buffer[1..]);
			return new((byte)buffer[1]);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}

	ValueTask ISystemManagementBus.WriteByteAsync(byte address, byte command, byte value)
	{
		try
		{
			_pawnIo.Execute("ioctl_i801_write_byte_data\0"u8, [address, command, value], []);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<byte> ISystemManagementBus.ReadByteAsync(byte address, byte command)
	{
		try
		{
			Span<ulong> buffer = stackalloc ulong[3];
			buffer[0] = address;
			buffer[1] = command;
			_pawnIo.Execute("ioctl_i801_read_byte_data\0"u8, buffer[..2], buffer[2..]);
			return new((byte)buffer[2]);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}

	ValueTask ISystemManagementBus.WriteWordAsync(byte address, byte command, ushort value)
	{
		try
		{
			_pawnIo.Execute("ioctl_i801_write_word_data\0"u8, [address, command, value], []);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
		return ValueTask.CompletedTask;
	}

	ValueTask<ushort> ISystemManagementBus.ReadWordAsync(byte address, byte command)
	{
		try
		{
			Span<ulong> buffer = stackalloc ulong[3];
			buffer[0] = address;
			buffer[1] = command;
			_pawnIo.Execute("ioctl_i801_read_word_data\0"u8, buffer[..2], buffer[2..]);
			return new((ushort)buffer[2]);
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}

	ValueTask ISystemManagementBus.WriteBlockAsync(byte address, byte command, ReadOnlyMemory<byte> data)
	{
		try
		{
			if (data.Length > 32) throw new ArgumentException(null, nameof(data));
			//Span<ulong> buffer = stackalloc ulong[3 + ((data.Length + 7) >>> 3)];
			Span<ulong> buffer = stackalloc ulong[7];
			buffer[0] = address;
			buffer[1] = command;
			buffer[2] = (uint)data.Length;
			data.Span.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, byte>(ref buffer[3]), data.Length));
			_pawnIo.Execute("ioctl_i801_write_block_data\0"u8, buffer, []);
			return ValueTask.CompletedTask;
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}

	ValueTask<byte[]> ISystemManagementBus.ReadBlockAsync(byte address, byte command)
	{
		try
		{
			Span<ulong> buffer = stackalloc ulong[7];
			buffer[0] = address;
			buffer[1] = command;
			_pawnIo.Execute("ioctl_i801_read_block_data\0"u8, buffer[..2], buffer[2..]);
			ulong length = buffer[2];
			if (length > 32) throw new InvalidDataException();
			return new(MemoryMarshal.CreateSpan(ref Unsafe.As<ulong, byte>(ref buffer[3]), (int)length).ToArray());
		}
		catch (COMException ex) when ((uint)ex.ErrorCode == ErrorNoSuchDevice)
		{
			throw new SystemManagementBusDeviceNotFoundException(address);
		}
	}
}
