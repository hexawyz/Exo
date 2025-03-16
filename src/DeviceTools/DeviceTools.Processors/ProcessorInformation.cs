using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Processors;

public sealed class ProcessorPackageInformation
{
	public static unsafe ImmutableArray<ProcessorPackageInformation> GetAll()
	{
		uint length = 0;
		NativeMethods.GetLogicalProcessorInformationEx(0xFFFF, null, &length);
		if (Marshal.GetLastWin32Error() != NativeMethods.ErrorInsufficientBuffer)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		return length <= 4096 ? GetAllFromStack(length) : GetAllFromArray(length);
	}

	private static unsafe ImmutableArray<ProcessorPackageInformation> GetAllFromStack(uint length)
	{
		byte* buffer = stackalloc byte[(int)length];
		if (NativeMethods.GetLogicalProcessorInformationEx(0xFFFF, buffer, &length) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
		return GetAll(new ReadOnlySpan<byte>(buffer, (int)length));
	}

	private static unsafe ImmutableArray<ProcessorPackageInformation> GetAllFromArray(uint length)
	{
		var buffer = new byte[length];
		fixed (byte* bufferPointer = buffer)
		{
			if (NativeMethods.GetLogicalProcessorInformationEx(0xFFFF, bufferPointer, &length) == 0)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
		}
		return GetAll(buffer.AsSpan(0, (int)length));
	}

	private static unsafe ImmutableArray<ProcessorPackageInformation> GetAll(ReadOnlySpan<byte> buffer)
	{
		// Reorganize the data in temporary lists so that we can process them properly afterwards.
		var packageData = new List<((ulong Mask, ushort Group)[] Groups, ushort MinGroup, byte MinNumber)>();
		var coreData = new List<(bool HasSmt, byte EfficiencyClass, ulong Mask, ushort Group)>();

		while (buffer.Length > 0)
		{
			ref var info = ref Unsafe.As<byte, NativeMethods.SystemLogicalProcessorInformationEx>(ref Unsafe.AsRef(in buffer[0]));
			if (info.Relationship is LogicalProcessorRelationship.ProcessorCore)
			{
				coreData.Add(((info.Processor.Flags & 1) != 0, info.Processor.EfficiencyClass, info.Processor.FirstAffinityGroup.Mask, info.Processor.FirstAffinityGroup.Group));
			}
			else if (info.Relationship is LogicalProcessorRelationship.ProcessorPackage)
			{
				var groups = new (ulong, ushort)[info.Processor.AffinityGroupCount];
				// With this "complicated" code, we guarantee that bounds will be checked.
				var sourceGroups = MemoryMarshal.Cast<byte, NativeMethods.GroupAffinity>
				(
					buffer.Slice
					(
						(int)Unsafe.ByteOffset(ref Unsafe.AsRef(in buffer[0]), ref Unsafe.As<NativeMethods.GroupAffinity, byte>(ref info.Processor.FirstAffinityGroup)),
						groups.Length * Unsafe.SizeOf<NativeMethods.GroupAffinity>()
					)
				);
				ushort minGroup = ushort.MaxValue;
				byte minNumber = byte.MaxValue;
				for (int i = 0; i < sourceGroups.Length; i++)
				{
					ref readonly var group = ref sourceGroups[i];
					groups[i] = (group.Mask, group.Group);
					if (group.Group < minGroup)
					{
						minGroup = group.Group;
						minNumber = (byte)BitOperations.TrailingZeroCount(group.Mask);
					}
				}
				packageData.Add((groups, minGroup, minNumber));
			}
			buffer = buffer[(int)info.Size..];
		}

		// Strictly ensure that the processors are ordered.
		// I assume that they would be, but because I can't guarantee they are, it is better to do it here.
		packageData.Sort
		(
			static (x, y) =>
			{
				int result = Comparer<ushort>.Default.Compare(x.MinGroup, y.MinGroup);
				if (result == 0)
				{
					result = Comparer<byte>.Default.Compare(x.MinNumber, y.MinNumber);
				}
				return result;
			}
		);

		var packages = new ProcessorPackageInformation[packageData.Count];
		for (int i = 0; i < packageData.Count; i++)
		{
			var package = packageData[i];
			if (package.Groups.Length == 1)
			{
				var group = package.Groups[0];
				packages[i] = new(packageData.Count == 1, group.Mask, group.Group, coreData);
			}
			else
			{
				packages[i] = new(package.Groups, coreData);
			}
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(packages);
	}

	private readonly ImmutableArray<ProcessorCoreInformation> _cores;
	private readonly ImmutableArray<ProcessorGroupAffinity> _groupAffinities;

	private ProcessorPackageInformation(bool isUniquePackage, ulong mask, ushort group, List<(bool HasSmt, byte EfficiencyClass, ulong Mask, ushort Group)> allCores)
	{
		if (isUniquePackage)
		{
			var cores = new ProcessorCoreInformation[allCores.Count];
			for (int i = 0; i < allCores.Count; i++)
			{
				var core = allCores[i];
				cores[i] = new(this, new(core.Mask, core.Group), core.HasSmt, core.EfficiencyClass);
			}
			Sort(cores);
			_cores = ImmutableCollectionsMarshal.AsImmutableArray(cores);
		}
		else
		{
			// If there are multiple threads per processor, we will necessarily overcommit here, but I don't really see a way to avoid this.
			var cores = new ProcessorCoreInformation[BitOperations.PopCount(mask)];
			uint count = 0;
			for (int i = 0; i < allCores.Count; i++)
			{
				var core = allCores[i];
				if (core.Group == group && (core.Mask & mask) == core.Mask)
				{
					cores[count++] = new(this, new(core.Mask, core.Group), core.HasSmt, core.EfficiencyClass);
				}
			}
			if (count < (uint)cores.Length)
			{
				cores = cores[..(int)count];
			}
			Sort(cores);
			_cores = ImmutableCollectionsMarshal.AsImmutableArray(cores);
		}
		_groupAffinities = [new(mask, group)];
	}

	private ProcessorPackageInformation((ulong Mask, ushort Group)[] groups, List<(bool HasSmt, byte EfficiencyClass, ulong Mask, ushort Group)> allCores)
	{
		var groupAffinities = new ProcessorGroupAffinity[groups.Length];
		uint count = 0;
		for (int i = 0; i < groups.Length; i++)
		{
			ref var group = ref groups[i];
			count += (uint)BitOperations.PopCount(groups[i].Mask);
			groupAffinities[i] = new(group.Mask, group.Group);
		}

		// Same as in the single-group version, we can overcommit if cores have SMT.
		var cores = new ProcessorCoreInformation[count];
		count = 0;
		for (int i = 0; i < allCores.Count; i++)
		{
			var core = allCores[i];

			for (int j = 0; j < groups.Length; j++)
			{
				ref var group = ref groups[i];
				if (core.Group == group.Group && (core.Mask & group.Mask) == core.Mask)
				{
					cores[count++] = new(this, new(core.Mask, core.Group), core.HasSmt, core.EfficiencyClass);
					break;
				}
			}
		}
		if (count < (uint)cores.Length)
		{
			cores = cores[..(int)count];
		}
		Sort(cores);
		_cores = ImmutableCollectionsMarshal.AsImmutableArray(cores);
		_groupAffinities = ImmutableCollectionsMarshal.AsImmutableArray(groupAffinities);
	}

	private static void Sort(ProcessorCoreInformation[] cores)
		=> Array.Sort
		(
			cores,
			static (x, y) =>
			{
				int result = Comparer<ushort>.Default.Compare(x.GroupAffinity.Group, y.GroupAffinity.Group);
				if (result == 0)
				{
					result = Comparer<byte>.Default.Compare((byte)BitOperations.TrailingZeroCount(x.GroupAffinity.Mask), (byte)BitOperations.TrailingZeroCount(y.GroupAffinity.Mask));
				}
				return result;
			}
		);

	public ImmutableArray<ProcessorCoreInformation> Cores => _cores;
	public ImmutableArray<ProcessorGroupAffinity> GroupAffinities => _groupAffinities;
}

public sealed class ProcessorCoreInformation
{
	internal ProcessorCoreInformation(ProcessorPackageInformation package, ProcessorGroupAffinity groupAffinity, bool hasSimultaneousMultiThreading, byte efficiencyClass)
	{
		Package = package;
		GroupAffinity = groupAffinity;
		HasSimultaneousMultiThreading = hasSimultaneousMultiThreading;
		EfficiencyClass = efficiencyClass;
	}

	public ProcessorPackageInformation Package { get; }
	public ProcessorGroupAffinity GroupAffinity { get; }
	public bool HasSimultaneousMultiThreading { get; }
	public byte EfficiencyClass { get; }
}
