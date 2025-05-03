using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo;

public ref struct BufferWriter
{
	public static uint GetVariableLength(ushort value) => (64 - (uint)BitOperations.LeadingZeroCount(value)) / 7;
	public static uint GetVariableLength(uint value) => (64 - (uint)BitOperations.LeadingZeroCount(value)) / 7;
	public static uint GetVariableLength(ulong value) => (64 - (uint)BitOperations.LeadingZeroCount(value)) / 7;

	private ref byte _current;
	private readonly ref readonly byte _end;
	private readonly ref readonly byte _start;

	[Obsolete]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public BufferWriter() => throw new InvalidOperationException();

	public BufferWriter(Span<byte> buffer)
	{
		_current = ref MemoryMarshal.GetReference(buffer);
		_end = ref Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(buffer), buffer.Length);
		_start = ref _current;
	}

	public readonly nuint Length => (nuint)Unsafe.ByteOffset(in _start, in _current);
	public readonly nuint RemainingLength => (nuint)Unsafe.ByteOffset(in _current, in _end);

	public void Write(byte value)
	{
		if (RemainingLength < 1) throw new EndOfStreamException();
		_current = value;
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 1);
	}

	// This one method is purely here for correctness, as Guids will be read using new Guid(ReadOnlySpan<byte>).
	// In practice, code should always run on little endian systems, so it won't make a difference.
	public void Write(Guid value)
	{
		if (RemainingLength < 16) throw new EndOfStreamException();
		value.TryWriteBytes(MemoryMarshal.CreateSpan(ref _current, 16));
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), 16);
	}

	public void Write(bool value) => Write(value ? (byte)1 : (byte)0);

	public void Write<T>(T value) where T : unmanaged
	{
		if (RemainingLength < (nuint)Unsafe.SizeOf<T>()) throw new EndOfStreamException();

		Unsafe.WriteUnaligned(ref _current, value);
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (uint)Unsafe.SizeOf<T>());
	}

	public void Write(ReadOnlySpan<byte> value)
	{
		if (RemainingLength < (nuint)value.Length) throw new EndOfStreamException();

		Unsafe.CopyBlock(ref _current, ref MemoryMarshal.GetReference(value), (uint)value.Length);
		_current = ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _current), (uint)value.Length);
	}

	public void WriteVariableBytes(ReadOnlySpan<byte> data)
	{
		var writer = this;
		writer.WriteVariable((uint)data.Length);
		if ((uint)data.Length > writer.RemainingLength) throw new EndOfStreamException();
		data.CopyTo(MemoryMarshal.CreateSpan(ref writer._current, data.Length));
		_current = ref Unsafe.AddByteOffset(ref writer._current, (uint)data.Length);
	}

	public void WriteVariableString(ReadOnlySpan<char> text)
	{
		// If the string has a small length, we know for sure that the length will fit in a single byte.
		// In that case, we can write everything directly to the buffer without fearing for too many problems.
		if (text.Length > 42)
		{
			WriteVariableStringSlow(text);
			return;
		}
		// We can easily check for a minimum length preemptively.
		if (RemainingLength < (uint)(text.Length + 1)) throw new EndOfStreamException();
		int length = Encoding.UTF8.GetBytes(text, MemoryMarshal.CreateSpan(ref Unsafe.AddByteOffset(ref _current, 1), (int)(RemainingLength - 1)));
		_current = (byte)length;
		_current = ref Unsafe.AddByteOffset(ref _current, (nuint)(uint)length + 1);
	}

	private void WriteVariableStringSlow(ReadOnlySpan<char> text)
	{
		// There isn't a much better alternative than parsing the whole string to determine the length here.
		// Better heuristics in the caller can maybe avoid this but that's honestly unsure.
		int length = Encoding.UTF8.GetByteCount(text);
		// Clone the writer to avoid any update to the current state until everything has been written.
		var writer = this;
		writer.WriteVariable((uint)length);
		if ((uint)length > writer.RemainingLength) throw new EndOfStreamException();
		Encoding.UTF8.GetBytes(text, MemoryMarshal.CreateSpan(ref writer._current, length));
		_current = ref Unsafe.AddByteOffset(ref writer._current, (uint)length);
	}

	public void WriteVariable(ushort value) => WriteVariableUIntPtr(value);

	public void WriteVariable(uint value) => WriteVariableUIntPtr(value);

	public void WriteVariable(ulong value)
	{
		if (nuint.Size == 8)
		{
			WriteVariableUIntPtr((nuint)value);
			return;
		}

		ulong v = value;

		while (true)
		{
			ulong w = v >>> 7;
			v &= 0x7F;
			if (w != 0)
			{
				Write((byte)(0x80 | v));
				v = w;
			}
			else
			{
				Write((byte)v);
				break;
			}
		}
	}

	public void WriteVariableUIntPtr(nuint value)
	{
		nuint v = value;

		while (true)
		{
			nuint w = v >>> 7;
			v &= 0x7F;
			if (w != 0)
			{
				Write((byte)(0x80 | v));
				v = w;
			}
			else
			{
				Write((byte)v);
				break;
			}
		}
	}
}
