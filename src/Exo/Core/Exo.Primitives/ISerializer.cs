using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Exo;

public interface ISerializer<T>
	where T : struct
{
	/// <summary>Tries to get the exact size that will be required to serialize the specified type.</summary>
	/// <remarks>
	/// <para>
	/// When possible and cheap enough, implementations should compute the exact size that will be required to hold the serialized data for the specified value.
	/// If the data size is known in advance, callers will be able to pre-allocate a buffer of the exact required size to hold the data.
	/// Otherwise, serialization will be done within an intermediate buffer.
	/// </para>
	/// <para>
	/// The base implementation of this method assumes straight binary representation for unmanaged types (with host endianness) and will return <see cref="Unsafe.SizeOf{T}"/> in <paramref name="size"/>.
	/// </para>
	/// <para>
	/// Implementations returning <see langword="false"/> can return a recommended serialization buffer size in <paramref name="size"/>.
	/// If <paramref name="size"/> is left at <c>0</c>, a default buffer size will be used.
	/// </para>
	/// </remarks>
	/// <param name="value"></param>
	/// <param name="size"></param>
	/// <returns><see langword="true"/> if the exact size required for serialization has been returned in <paramref name="size"/>; otherwise <see langword="false"/>.</returns>
	static virtual bool TryGetSize(in T value, out uint size)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			size = default;
			return false;
		}
		size = (uint)Unsafe.SizeOf<T>();
		return true;
	}

	/// <summary>Serializes the specified value.</summary>
	/// <remarks>The default implementation provides straight binary representation for unmanaged types. Other types are not supported.</remarks>
	/// <param name="writer"></param>
	/// <param name="value"></param>
	/// <exception cref="NotSupportedException"></exception>
	static virtual void Serialize(ref BufferWriter writer, in T value)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) throw new NotSupportedException();
		if (writer.RemainingLength < (nuint)Unsafe.SizeOf<T>()) throw new EndOfStreamException();
		writer.Write(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in value)), Unsafe.SizeOf<T>()));
		
	}

	/// <summary>Deserializes a value from the provided reader.</summary>
	/// <remarks>The default implementation provides straight binary representation for unmanaged types. Other types are not supported.</remarks>
	/// <param name="reader"></param>
	/// <param name="value"></param>
	/// <exception cref="NotSupportedException"></exception>
	/// <exception cref="EndOfStreamException"></exception>
	static virtual void Deserialize(ref BufferReader reader, out T value)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) throw new NotSupportedException();
		if (reader.RemainingLength < (nuint)Unsafe.SizeOf<T>()) throw new EndOfStreamException();
		Unsafe.SkipInit(out value);
		reader.Read(MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>()));
	}
}
