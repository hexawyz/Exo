using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware.Uefi;

public readonly struct EfiVariableNameAndValue
{
	private readonly byte[] _buffer;
	private readonly int _offset;
	private readonly int _length;

	private EfiVariableNameAndValue(byte[] buffer, int offset, int length)
	{
		_buffer = buffer;
		_offset = offset;
		_length = length;
	}

	public string Name
		=> EfiEnvironment.AsNullTerminated(MemoryMarshal.Cast<byte, char>(_buffer.AsSpan(_offset + Unsafe.SizeOf<NativeMethods.VariableNameAndValueFixedPart>(), GetNameMaxByteLength()))).ToString();

	public Guid VendorGuid
		=> EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameAndValueFixedPart>(_buffer, _offset).VendorGuid;

	public ReadOnlyMemory<byte> Value
	{
		get
		{
			ref readonly var header = ref EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameAndValueFixedPart>(_buffer, _offset);

			if (header.ValueOffset > 0 && header.ValueLength > 0)
			{
				return _buffer.AsMemory(_offset + (int)header.ValueOffset, (int)header.ValueLength);
			}

			return default;
		}
	}

	public EfiVariableAttributes Attributes => (EfiVariableAttributes)EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameAndValueFixedPart>(_buffer, _offset).Attributes;

	private int GetNameMaxByteLength()
		=> (EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameAndValueFixedPart>(_buffer, _offset).ValueOffset is uint o and not 0 ? (int)o : _length) - Unsafe.SizeOf<NativeMethods.VariableNameAndValueFixedPart>();

	internal static IEnumerable<EfiVariableNameAndValue> EnumerateAll()
	{
		SystemEnvironmentPrivilege.Initialize();

		uint bufferLength = EfiEnvironment.GetBufferLength(NativeMethods.VariableEnumerationInformationClass.Values);

		if (bufferLength == 0) return Enumerable.Empty<EfiVariableNameAndValue>();

		var buffer = EfiEnvironment.GetEnvironmentVariables(NativeMethods.VariableEnumerationInformationClass.Values, bufferLength);

		return EnumerateAllCore(buffer, (int)bufferLength);
	}

	private static IEnumerable<EfiVariableNameAndValue> EnumerateAllCore(byte[] buffer, int bufferLength)
	{
		int offset = 0;

		while (true)
		{
			int nextEntryOffset = (int)EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameAndValueFixedPart>(buffer, offset).NextEntryOffset;

			Debug.Assert((offset & 0x3) == 0, "The data offset is not 32-bit aligned.");

			int entryLength = nextEntryOffset > 0 ? nextEntryOffset : bufferLength - offset;

			Debug.Assert(entryLength >= Unsafe.SizeOf<NativeMethods.VariableNameAndValueFixedPart>() + 4, "The data is not long enough.");

			yield return new EfiVariableNameAndValue(buffer, offset, entryLength);

			// Entries must contain at least the variable name.
			if (nextEntryOffset < Unsafe.SizeOf<NativeMethods.VariableNameAndValueFixedPart>() + 4) break;

			offset += nextEntryOffset;
		}
	}
}
