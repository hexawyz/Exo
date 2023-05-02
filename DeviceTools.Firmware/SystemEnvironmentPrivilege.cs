using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware;

internal static class SystemEnvironmentPrivilege
{
	static SystemEnvironmentPrivilege()
	{
		if (!NativeMethods.LookupPrivilegeValue(null, NativeMethods.SeSystemEnvironmentPrivilege, out int privilege))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		NativeMethods.ValidateNtStatus(NativeMethods.RtlAdjustPrivilege(privilege, true, false, out bool _));
	}

	public static void Initialize() { }
}
