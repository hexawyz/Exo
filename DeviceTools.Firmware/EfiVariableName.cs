using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware.Uefi;

public readonly struct EfiVariableName
{
	private readonly byte[] _buffer;
	private readonly int _offset;
	private readonly int _length;

	private EfiVariableName(byte[] buffer, int offset, int length)
	{
		_buffer = buffer;
		_offset = offset;
		_length = length;
	}

	public string Name
		=> EfiEnvironment.AsNullTerminated(MemoryMarshal.Cast<byte, char>(_buffer.AsSpan(_offset + Unsafe.SizeOf<NativeMethods.VariableNameFixedPart>(), GetNameMaxByteLength()))).ToString();

	public Guid VendorGuid
		=> EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameFixedPart>(_buffer, _offset).VendorGuid;

	private int GetNameMaxByteLength()
		=> _length - Unsafe.SizeOf<NativeMethods.VariableNameFixedPart>();

	internal static IEnumerable<EfiVariableName> EnumerateAll()
	{
		SystemEnvironmentPrivilege.Initialize();

		uint bufferLength = EfiEnvironment.GetBufferLength(NativeMethods.VariableEnumerationInformationClass.Names);

		if (bufferLength == 0) return Enumerable.Empty<EfiVariableName>();

		var buffer = EfiEnvironment.GetEnvironmentVariables(NativeMethods.VariableEnumerationInformationClass.Names, bufferLength);

		return EnumerateAllCore(buffer, (int)bufferLength);
	}

	private static IEnumerable<EfiVariableName> EnumerateAllCore(byte[] buffer, int bufferLength)
	{
		int offset = 0;

		while (true)
		{
			int nextEntryOffset = (int)EfiEnvironment.UnsafeGetHeader<NativeMethods.VariableNameFixedPart>(buffer, offset).NextEntryOffset;

			Debug.Assert((offset & 0x3) == 0, "The data offset is not 32-bit aligned.");

			int entryLength = nextEntryOffset > 0 ? nextEntryOffset : bufferLength - offset;

			Debug.Assert(entryLength >= Unsafe.SizeOf<NativeMethods.VariableNameFixedPart>() + 4, "The data is not long enough.");

			yield return new EfiVariableName(buffer, offset, entryLength);

			// Entries must contain at least the variable name.
			if (nextEntryOffset < Unsafe.SizeOf<NativeMethods.VariableNameFixedPart>() + 4) break;

			offset += nextEntryOffset;
		}
	}
}
