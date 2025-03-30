using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Ipc;

public ref struct BufferReader
{
	private ref readonly byte _current;
	private readonly ref readonly byte _end;

	[Obsolete]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public BufferReader() => throw new InvalidOperationException();

	public BufferReader(ReadOnlySpan<byte> buffer)
	{
		_current = ref MemoryMarshal.GetReference(buffer);
		_end = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(buffer), buffer.Length);
	}

	public readonly nuint RemainingLength => (nuint)Unsafe.ByteOffset(in _current, in _end);

	public byte ReadByte()
	{
		if (!Unsafe.IsAddressLessThan(in _current, in _end)) throw new EndOfStreamException();

		byte value = _current;
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 1);
		return value;
	}

	public T Read<T>() where T : unmanaged
	{
		if (RemainingLength < (nuint)Unsafe.SizeOf<T>()) throw new EndOfStreamException();

		T value = Unsafe.ReadUnaligned<T>(in _current);
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (uint)Unsafe.SizeOf<T>());
		return value;
	}

	public void Read(Span<byte> destination)
	{
		if (destination.Length == 0) return;
		if ((uint)destination.Length > RemainingLength) throw new EndOfStreamException();

		MemoryMarshal.CreateReadOnlySpan(in _current, destination.Length).CopyTo(destination);
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (uint)destination.Length);
	}

	public ushort ReadVariableUInt16() => (ushort)ReadVariableUIntPtr(14);

	public uint ReadVariableUInt32() => (uint)ReadVariableUIntPtr(28);

	public ulong ReadVariableUInt64()
	{
		if (nuint.Size > 8)
		{
			return ReadVariableUIntPtr(63);
		}

		byte b = ReadByte();
		if ((sbyte)b < 0)
		{
			ulong v = (uint)b & 0x7F;
			nuint shift = 7;
			while (true)
			{
				b = ReadByte();
				if ((sbyte)b >= 0 || shift >= 63)
				{
					return v | (ulong)b << (int)shift;
				}
				else
				{
					v = v | ((ulong)b & 0x7F) << (int)shift;
					shift += 7;
				}
			}
		}
		else
		{
			return b;
		}
	}

	// Counting on the JIT / AOT to optimize this computation. Otherwise, this will be slow.
	public nuint ReadVariableUIntPtr() => ReadVariableUIntPtr((nuint)(nuint.Size * 8) - (nuint)(nuint.Size * 8) % 7);

	public nuint ReadVariableUIntPtr(nuint lastShift)
	{
		byte b = ReadByte();
		if ((sbyte)b < 0)
		{
			nuint v = (uint)b & 0x7F;
			nuint shift = 7;
			while (true)
			{
				b = ReadByte();
				if ((sbyte)b >= 0 || shift >= lastShift)
				{
					return v | (nuint)b << (int)shift;
				}
				else
				{
					v = v | ((nuint)b & 0x7F) << (int)shift;
					shift += 7;
				}
			}
		}
		else
		{
			return b;
		}
	}

	public string? ReadVariableString()
	{
		var length = ReadVariableUInt32();
		if (length == 0) return null;
		return ReadString(length);
	}

	public string ReadString(uint length)
	{
		if (length == 0) return "";
		if (length > RemainingLength) throw new EndOfStreamException();

		string value = Encoding.UTF8.GetString(MemoryMarshal.CreateReadOnlySpan(in _current, (int)length));
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (nint)(nuint)length);
		return value;
	}

	public byte[]? ReadVariableBytes()
	{
		var length = ReadVariableUInt32();
		if (length == 0) return null;
		return ReadBytes(length);
	}

	public byte[] ReadBytes(uint length)
	{
		if (length == 0) return [];
		if (length > RemainingLength) throw new EndOfStreamException();

		var value = MemoryMarshal.CreateReadOnlySpan(in _current, (int)length).ToArray();
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), length);
		return value;
	}

	public Guid ReadGuid()
	{
		if (RemainingLength < 16) throw new EndOfStreamException();

		var value = new Guid(MemoryMarshal.CreateReadOnlySpan(in _current, 16));
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 16);
		return value;
	}
}
