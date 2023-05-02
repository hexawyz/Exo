using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware.Uefi;

public static unsafe class EfiEnvironment
{
	/// <summary>Enumerates EFI variable names.</summary>
	/// <remarks>Values that will be enumerated are computed when this method is called. As such, the returned enumerable will always return the same informations if enumerated multiple times.</remarks>
	/// <returns>An enumerable of EFI variable names.</returns>
	public static IEnumerable<EfiVariableName> EnumerateVariableNames()
		=> EfiVariableName.EnumerateAll();

	/// <summary>Enumerates EFI variables with their name and current value.</summary>
	/// <remarks>Values that will be enumerated are computed when this method is called. As such, the returned enumerable will always return the same informations if enumerated multiple times.</remarks>
	/// <returns>An enumerable of EFI variable names and values.</returns>
	public static IEnumerable<EfiVariableNameAndValue> EnumerateVariableValues()
		=> EfiVariableNameAndValue.EnumerateAll();

	public static byte[] GetVariable(ReadOnlySpan<char> name, Guid vendorGuid)
	{
		SystemEnvironmentPrivilege.Initialize();

		uint bufferLength = 0;
		fixed (char* namePointer = name)
		{
			var unicodeString = new NativeMethods.UnicodeString
			{
				Buffer = (ushort*)namePointer,
				Length = (ushort)(name.Length << 1),
				MaximumLength = (ushort)((name.Length + 1) << 1)
			};
			uint status = NativeMethods.NtQuerySystemEnvironmentValueEx(unicodeString, vendorGuid, null, ref bufferLength, out uint attributes);
			while (true)
			{
				if (status != NativeMethods.NtStatusBufferTooSmall) NativeMethods.ValidateNtStatus(status);
				var buffer = new byte[bufferLength];
				fixed (byte* bufferPointer = buffer)
				{
					status = NativeMethods.NtQuerySystemEnvironmentValueEx(unicodeString, vendorGuid, bufferPointer, ref bufferLength, out attributes);
					if (status == 0)
					{
						if (bufferLength != (uint)buffer.Length)
						{
							Array.Resize(ref buffer, (int)bufferLength);
						}
						return buffer;
					}
				}
			}
		}
	}

	public static void SetVariable(string name, Guid vendorGuid, byte[] value, EfiVariableAttributes attributes = EfiVariableAttributes.NonVolatile | EfiVariableAttributes.BootServiceAccess | EfiVariableAttributes.RuntimeAccess)
		=> SetVariable(name.AsSpan(), vendorGuid, value.AsSpan(), attributes);

	public static void SetVariable(ReadOnlySpan<char> name, Guid vendorGuid, ReadOnlySpan<byte> value, EfiVariableAttributes attributes = EfiVariableAttributes.NonVolatile | EfiVariableAttributes.BootServiceAccess | EfiVariableAttributes.RuntimeAccess)
	{
		SystemEnvironmentPrivilege.Initialize();

		fixed (char* namePointer = name)
		{
			var unicodeString = new NativeMethods.UnicodeString
			{
				Buffer = (ushort*)namePointer,
				Length = (ushort)(name.Length << 1),
				MaximumLength = (ushort)((name.Length + 1) << 1),
			};

			fixed (byte* valuePointer = value)
			{
				NativeMethods.ValidateNtStatus(NativeMethods.NtSetSystemEnvironmentValueEx(unicodeString, vendorGuid, valuePointer, (uint)value.Length, (uint)attributes));
			}
		}
	}

	internal static unsafe uint GetBufferLength(NativeMethods.VariableEnumerationInformationClass informationClass)
	{
		uint bufferLength = 0;
		uint status = NativeMethods.NtEnumerateSystemEnvironmentValuesEx(informationClass, null, ref bufferLength);

		if (status != NativeMethods.NtStatusBufferTooSmall)
		{
			NativeMethods.ValidateNtStatus(status);
		}

		return bufferLength;
	}

	internal static unsafe byte[] GetEnvironmentVariables(NativeMethods.VariableEnumerationInformationClass informationClass, uint bufferLength)
	{
		byte[] buffer = new byte[bufferLength];

		fixed (byte* ptr = buffer)
		{
			NativeMethods.ValidateNtStatus(NativeMethods.NtEnumerateSystemEnvironmentValuesEx(informationClass, ptr, ref bufferLength));
		}

		return buffer;
	}

	// This method bypasses upper-bound checking. Calling code must ensure that everything is in range before calling this.
	internal static ref readonly T UnsafeGetHeader<T>(byte[] buffer, int offset) where T : unmanaged
		=> ref Unsafe.As<byte, T>(ref Unsafe.AddByteOffset(ref MemoryMarshal.GetArrayDataReference(buffer), offset));

	internal static ReadOnlySpan<char> AsNullTerminated(ReadOnlySpan<char> span)
		=> span.IndexOf('\0') is int index and >= 0 ? span.Slice(0, index) : span;
}
