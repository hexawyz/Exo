using System.Runtime.InteropServices;
using System.Security;

namespace DeviceTools.Processors;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	public const uint ErrorInsufficientBuffer = 122;
	public const byte LtpPcSmp = 1;

	[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	public static extern nint GetCurrentThread();
	//[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	//public static extern nuint SetThreadAffinityMask(nuint threadHandle, nuint threadAffinityMask);
	[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint SetThreadGroupAffinity(nint threadHandle, GroupAffinity* groupAffinity, GroupAffinity* previousGroupAffinity);
	[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint GetLogicalProcessorInformationEx(int relationshipType, void* buffer, uint* returnedLength);

	public struct GroupAffinity
	{
		public nuint Mask;
		public ushort Group;
#pragma warning disable IDE0044
		private ushort _reserved0;
		private ushort _reserved1;
		private ushort _reserved2;
#pragma warning restore IDE0044
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct SystemLogicalProcessorInformationEx
	{
		[FieldOffset(0)]
		public LogicalProcessorRelationship Relationship;
		[FieldOffset(4)]
		public uint Size;
		[FieldOffset(8)]
		public ProcessorRelationship Processor;
		//[FieldOffset(8)]
		//public * NumaNode;
		//[FieldOffset(8)]
		//public * Cache;
		//[FieldOffset(8)]
		//public * Group;
	}

	public struct ProcessorRelationship
	{
		public byte Flags;
		public byte EfficiencyClass;
#pragma warning disable IDE0044
		private byte _reserved00;
		private byte _reserved01;
		private byte _reserved02;
		private byte _reserved03;
		private byte _reserved04;
		private byte _reserved05;
		private byte _reserved06;
		private byte _reserved07;
		private byte _reserved08;
		private byte _reserved09;
		private byte _reserved0A;
		private byte _reserved0B;
		private byte _reserved0C;
		private byte _reserved0D;
		private byte _reserved0E;
		private byte _reserved0F;
		private byte _reserved10;
		private byte _reserved11;
		private byte _reserved12;
		private byte _reserved13;
#pragma warning restore IDE0044
		public ushort AffinityGroupCount;
		public GroupAffinity FirstAffinityGroup;
	}
}

public enum LogicalProcessorRelationship
{
	ProcessorCore,
	NumaNode,
	Cache,
	ProcessorPackage,
	Group,
	ProcessorDie,
	NumaNodeEx,
	ProcessorModule,
}
