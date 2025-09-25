using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Security;

namespace DeviceTools.Firmware;

[SuppressUnmanagedCodeSecurity]
internal static unsafe class NativeMethods
{
	public const uint NtStatusBufferTooSmall = 0xC0000023;

	[StructLayout(LayoutKind.Sequential)]
	public struct UnicodeString
	{
		public ushort Length;
		public ushort MaximumLength;
		public ushort* Buffer;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 0)]
	public struct VariableNameAndValueFixedPart
	{
		public uint NextEntryOffset;
		public uint ValueOffset;
		public uint ValueLength;
		public uint Attributes;
		public Guid VendorGuid;
		// Followed by name and value
	}

	[StructLayout(LayoutKind.Sequential, Pack = 0)]
	public struct VariableNameFixedPart
	{
		public uint NextEntryOffset;
		public Guid VendorGuid;
		// Followed by name and value
	}

	public enum VariableEnumerationInformationClass : uint
	{
		Names = 1,
		Values = 2,
	}

	public const string SeSystemEnvironmentPrivilege = "SeSystemEnvironmentPrivilege";

	[DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "SetFirmwareEnvironmentVariableW", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern bool SetFirmwareEnvironmentVariable(string name, string guid, void* value, uint size);

	//[DllImport("ntdll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	//public static extern uint NtQuerySystemEnvironmentValue(in UnicodeString variableName, ushort* variableValue, ushort valueLength, out ushort returnLength);
	//[DllImport("ntdll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	//public static extern uint NtSetSystemEnvironmentValue(in UnicodeString variableName, in UnicodeString variableValue);

	[DllImport("ntdll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint NtQuerySystemEnvironmentValueEx(in UnicodeString variableName, in Guid vendorGuid, void* value, ref uint valueLength, out uint attributes);
	[DllImport("ntdll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint NtSetSystemEnvironmentValueEx(in UnicodeString variableName, in Guid vendorGuid, void* value, uint valueLength, uint attributes);

	[DllImport("ntdll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint NtEnumerateSystemEnvironmentValuesEx(VariableEnumerationInformationClass informationClass, void* buffer, ref uint bufferLength);

	[DllImport("advapi32", CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern bool LookupPrivilegeValue(string? systemName, string name, out ulong privilege);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint RtlAdjustPrivilege(ulong privilege, bool shouldEnablePrivilege, bool isThreadPrivilege, out bool previousValue);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint RtlNtStatusToDosError(uint status);

	[DebuggerHidden]
	[StackTraceHidden]
	public static void ValidateNtStatus(uint status)
	{
		if (status != 0)
		{
			throw new Win32Exception((int)RtlNtStatusToDosError(status));
		}
	}

	public enum SystemInformationClass : uint
	{
		SystemBasicInformation = 0,
		SystemProcessorInformation = 1,
		SystemPerformanceInformation = 2,
		SystemTimeOfDayInformation = 3,
		SystemPathInformation = 4,
		SystemProcessInformation = 5,
		SystemCallCountInformation = 6,
		SystemDeviceInformation = 7,
		SystemProcessorPerformanceInformation = 8,
		SystemFlagsInformation = 9,
		SystemCallTimeInformation = 10,
		SystemModuleInformation = 11,
		SystemLocksInformation = 12,
		SystemStackTraceInformation = 13,
		SystemPagedPoolInformation = 14,
		SystemNonPagedPoolInformation = 15,
		SystemHandleInformation = 16,
		SystemObjectInformation = 17,
		SystemPageFileInformation = 18,
		SystemVdmInstemulInformation = 19,
		SystemVdmBopInformation = 20,
		SystemFileCacheInformation = 21,
		SystemPoolTagInformation = 22,
		SystemInterruptInformation = 23,
		SystemExceptionInformation = 33,
		SystemRegistryQuotaInformation = 37,
		SystemLookasideInformation = 45,

		SystemRegisterFirmwareTableInformationHandler = 75,
		SystemFirmwareTableInformation = 76,
	}

	[DllImport("kernel32", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern uint EnumSystemFirmwareTables(uint firmwareTableProviderSignature, void* pFirmwareTableEnumBuffer, uint bufferSize);

	[DllImport("kernel32", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern uint GetSystemFirmwareTable(uint firmwareTableProviderSignature, uint firmwareTableID, void* firmwareTableBuffer, uint bufferSize);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint NtQuerySystemInformation
	(
		SystemInformationClass systemInformationClass,
		void* systemInformation,
		uint systemInformationLength,
		uint* returnLength
	);

	//[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	//public static extern uint NtQuerySystemInformationEx
	//(
	//	SystemInformationClass systemInformationClass,
	//	void* inputBuffer,
	//	uint inputBufferLength,
	//	void* systemInformation,
	//	uint systemInformationLength,
	//	uint* returnLength
	//);
}
