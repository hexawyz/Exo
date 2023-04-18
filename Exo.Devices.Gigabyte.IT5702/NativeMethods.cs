using System.Runtime.InteropServices;
using System.Security;

namespace Exo.Devices.Gigabyte;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	public const string SeSystemEnvironmentPrivilege = "SeSystemEnvironmentPrivilege";

	[DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "SetFirmwareEnvironmentVariableW", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern unsafe bool SetFirmwareEnvironmentVariable(string name, string guid, void* value, uint size);

	[DllImport("kernel32", CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	public static extern bool LookupPrivilegeValue(string? systemName, string name, out int privilege);

	[DllImport("ntdll", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	public static extern uint RtlAdjustPrivilege(int privilege, bool shouldEnablePrivilege, bool isThreadPrivilege, out bool previousValue);
}
