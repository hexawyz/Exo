using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeviceTools.Firmware;

public static class Acpi
{
	private static byte[] GetTableFromRegistry(AcpiTableName tableName)
	{
		using var acpiKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\ACPI");
		if (acpiKey is null) throw new InvalidOperationException();
		using var tableKey = acpiKey.OpenSubKey(tableName.ToString());
		if (tableKey is null) throw new InvalidOperationException();
		using var subKey1 = OpenSingleSubKey(tableKey);
		using var subKey2 = OpenSingleSubKey(subKey1);
		using var subKey3 = OpenSingleSubKey(subKey2);
		return (byte[])(subKey3.GetValue("00000000") ?? throw new InvalidOperationException());
	}

	private static RegistryKey OpenSingleSubKey(RegistryKey key)
	{
		var subKeys = key.GetSubKeyNames();
		if (subKeys is null || subKeys.Length != 1) throw new InvalidOperationException();
		return key.OpenSubKey(subKeys[0]) ?? throw new InvalidOperationException();
	}

	public static unsafe AcpiTable[] GetTables()
	{
		const uint AcpiSignature = (((byte)'A' << 8 | (byte)'C') << 8 | (byte)'P') << 8 | (byte)'I';
		const uint SsdtSignature = (((byte)'T' << 8 | (byte)'D') << 8 | (byte)'S') << 8 | (byte)'S';

		Span<uint> tableIds = stackalloc uint[256];
		uint length = NativeMethods.EnumSystemFirmwareTables(AcpiSignature, Unsafe.AsPointer(ref MemoryMarshal.GetReference(tableIds)), (uint)tableIds.Length * sizeof(uint));

		if (length == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		var tables = new AcpiTable[length / sizeof(uint)];
		var buffer = new byte[1024 * 1024];
		int ssdtIndex = -1;
		fixed (byte* bufferPointer = buffer)
		{
			for (int i = 0; i < tables.Length; i++)
			{
				uint tableId = tableIds[i];

				if (tableId == SsdtSignature)
				{
					if (++ssdtIndex > 0)
					{
						tableId &= ~0xFF000000U;
						if (ssdtIndex < 10)
							tableId |= (uint)(byte)('0' + ssdtIndex) << 24;
						else if (ssdtIndex < 29)
							tableId |= (uint)(byte)('A' + ssdtIndex - 10) << 24;
						else
							throw new InvalidOperationException("Too many SSDTs.");

						tables[i] = new(new(tableIds[i]), (uint)ssdtIndex, GetTableFromRegistry(new(tableId)));
						continue;
					}
				}

				*(uint*)bufferPointer = AcpiSignature;
				((uint*)bufferPointer)[1] = 1;
				((uint*)bufferPointer)[2] = tableId;
				((uint*)bufferPointer)[3] = (uint)buffer.Length - 16;

				NativeMethods.ValidateNtStatus(NativeMethods.NtQuerySystemInformation(NativeMethods.SystemInformationClass.SystemFirmwareTableInformation, bufferPointer, (uint)buffer.Length, &length));
				length = ((uint*)bufferPointer)[3];
				//length = NativeMethods.GetSystemFirmwareTable(Signature, tableIds[i], bufferPointer, (uint)buffer.Length);

				if (length == 0)
				{
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}

				tables[i] = new(new(tableIds[i]), 0, buffer.AsSpan(16, (int)length).ToArray());
			}
		}

		return tables;
	}
}
