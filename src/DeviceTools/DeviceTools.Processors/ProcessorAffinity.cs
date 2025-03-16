using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Processors;

public static class ProcessorAffinity
{
	public static ProcessorGroupAffinity SetForCurrentThread(nuint mask, ushort group)
		=> SetForThread(NativeMethods.GetCurrentThread(), mask, group);

	public static void SetForCurrentThread(ImmutableArray<ProcessorGroupAffinity> groupAffinities)
		=> SetForThread(NativeMethods.GetCurrentThread(), groupAffinities);

	[SkipLocalsInit]
	public static unsafe ProcessorGroupAffinity SetForThread(nint threadHandle, nuint mask, ushort group)
	{
		var newGroupAffinity = new NativeMethods.GroupAffinity() { Mask = mask, Group = group };
		NativeMethods.GroupAffinity oldGroupAffinity = new();
		if (NativeMethods.SetThreadGroupAffinity(threadHandle, &newGroupAffinity, &oldGroupAffinity) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		return new(oldGroupAffinity.Mask, oldGroupAffinity.Group);
	}

	public static unsafe void SetForThread(nint threadHandle, ImmutableArray<ProcessorGroupAffinity> groupAffinities)
	{
		if (groupAffinities.IsDefault) throw new ArgumentNullException(nameof(groupAffinities));
		if ((uint)(groupAffinities.Length - 1) > 65535) throw new ArgumentException();
		// On 64-bit systems, NativeMethods.GroupAffinity and ProcessorGroupAffinity will have the same memory layout.
		if (nint.Size == 8)
		{
			fixed (ProcessorGroupAffinity* groupAffinitiesPointer = ImmutableCollectionsMarshal.AsArray(groupAffinities)!)
			{
				if (NativeMethods.SetThreadSelectedCpuSetMasks(threadHandle, (NativeMethods.GroupAffinity*)groupAffinitiesPointer, (ushort)groupAffinities.Length) == 0)
				{
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}
			}
		}
		else
		{
			SetForThreadWithExplicitConversion(threadHandle, groupAffinities);
		}
	}

	private static unsafe void SetForThreadWithExplicitConversion(nint threadHandle, ImmutableArray<ProcessorGroupAffinity> groupAffinities)
	{
		if (groupAffinities.Length < 64)
		{
			var nativeGroupAffinities = stackalloc NativeMethods.GroupAffinity[groupAffinities.Length];
			Convert(groupAffinities, new(nativeGroupAffinities, groupAffinities.Length));
			if (NativeMethods.SetThreadSelectedCpuSetMasks(threadHandle, nativeGroupAffinities, (ushort)groupAffinities.Length) == 0)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
		}
		else
		{
			var nativeGroupAffinities = new NativeMethods.GroupAffinity[groupAffinities.Length];
			Convert(groupAffinities, nativeGroupAffinities);
			fixed (ProcessorGroupAffinity* groupAffinitiesPointer = ImmutableCollectionsMarshal.AsArray(groupAffinities)!)
			{
				if (NativeMethods.SetThreadSelectedCpuSetMasks(threadHandle, (NativeMethods.GroupAffinity*)groupAffinitiesPointer, (ushort)groupAffinities.Length) == 0)
				{
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}
			}
		}
	}

	private static void Convert(ImmutableArray<ProcessorGroupAffinity> groupAffinities, Span<NativeMethods.GroupAffinity> nativeGroupAffinities)
	{
		for (int i = 0; i < groupAffinities.Length; i++)
		{
			ref readonly var src = ref ImmutableCollectionsMarshal.AsArray(groupAffinities)![i];
			nativeGroupAffinities[i] = new() { Mask = (nuint)src.Mask, Group = src.Group };
		}
	}
}
