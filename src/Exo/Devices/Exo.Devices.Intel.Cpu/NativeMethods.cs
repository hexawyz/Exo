using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Exo.Devices.Intel.Cpu;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	public static extern nint GetCurrentThread();
	//[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	//public static extern nuint SetThreadAffinityMask(nuint threadHandle, nuint threadAffinityMask);
	[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	private static extern unsafe uint SetThreadGroupAffinity(nint threadHandle, GroupAffinity* groupAffinity, GroupAffinity* previousGroupAffinity);

	private struct GroupAffinity
	{
		public nuint Mask;
		public ushort Group;
		private ushort _reserved0;
		private ushort _reserved1;
		private ushort _reserved2;
	}

	[SkipLocalsInit]
	public static unsafe (nuint Mask, ushort Group) SetThreadGroupAffinity(nint threadHandle, nuint mask, ushort group)
	{
		var newGroupAffinity = new GroupAffinity() { Mask = mask, Group = group };
		GroupAffinity oldGroupAffinity = new();
		if (SetThreadGroupAffinity(threadHandle, &newGroupAffinity, &oldGroupAffinity) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		return (oldGroupAffinity.Mask, oldGroupAffinity.Group);
	}
}
