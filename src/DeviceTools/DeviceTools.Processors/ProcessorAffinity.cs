using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Processors;

public sealed class ProcessorAffinity
{
	[SkipLocalsInit]
	public static unsafe (nuint Mask, ushort Group) SetForCurrentThread(nuint mask, ushort group)
		=> SetForThread(NativeMethods.GetCurrentThread(), mask, group);

	[SkipLocalsInit]
	public static unsafe (nuint Mask, ushort Group) SetForThread(nint threadHandle, nuint mask, ushort group)
	{
		var newGroupAffinity = new NativeMethods.GroupAffinity() { Mask = mask, Group = group };
		NativeMethods.GroupAffinity oldGroupAffinity = new();
		if (NativeMethods.SetThreadGroupAffinity(threadHandle, &newGroupAffinity, &oldGroupAffinity) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		return (oldGroupAffinity.Mask, oldGroupAffinity.Group);
	}
}
