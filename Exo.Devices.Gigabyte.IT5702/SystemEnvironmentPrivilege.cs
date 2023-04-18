using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Exo.Devices.Gigabyte;

internal static class SystemEnvironmentPrivilege
{
	static SystemEnvironmentPrivilege()
	{
		if (!NativeMethods.LookupPrivilegeValue(null, NativeMethods.SeSystemEnvironmentPrivilege, out int privilege))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		if (NativeMethods.RtlAdjustPrivilege(privilege, true, false, out bool _) != 0)
		{
			throw new Exception("Failed to enable SeSystemEnvironmentPrivilege.");
		}
	}

	public static void Initialize() { }
}
