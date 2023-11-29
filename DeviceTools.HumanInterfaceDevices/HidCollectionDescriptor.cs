using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.HumanInterfaceDevices;

public readonly struct HidCollectionDescriptor
{
	// For more details on the preparsed data structure, see this comment:
	// https://github.com/libusb/hidapi/pull/306#issuecomment-1385672740

	[StructLayout(LayoutKind.Sequential)]
	private readonly struct PreparsedDataHeader
	{
		public readonly int Signature1;
		public readonly int Signature2;
		public readonly ushort Usage;
		public readonly ushort UsagePage;
		public readonly uint PowerButtonMask;
		public readonly ChannelReportHeader Input;
		public readonly ChannelReportHeader Output;
		public readonly ChannelReportHeader Feature;
		public readonly ushort LinkCollectionArrayOffset;
		public readonly ushort LinkCollectionArrayLength;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ChannelReportHeader
	{
		public ushort Offset;
		public ushort Size;
		public ushort Index;
		public ushort ByteLen;
	}

	[StructLayout(LayoutKind.Explicit, Size = 104)]
	private readonly struct ChannelDescriptor
	{
		[FieldOffset(0)]
		public readonly ushort UsagePage;
		[FieldOffset(2)]
		public readonly byte ReportId;
		[FieldOffset(3)]
		public readonly byte BitOffset;
		[FieldOffset(4)]
		public readonly ushort ReportSize;
		[FieldOffset(6)]
		public readonly ushort ReportCount;
		[FieldOffset(8)]
		public readonly ushort ByteOffset;
		[FieldOffset(10)]
		public readonly ushort BitLength;
		[FieldOffset(12)]
		public readonly int BitField;
		[FieldOffset(16)]
		public readonly ushort ByteEnd;
		[FieldOffset(18)]
		public readonly ushort LinkCollection;
		[FieldOffset(20)]
		public readonly ushort LinkUsagePage;
		[FieldOffset(22)]
		public readonly ushort LinkUsage;
		[FieldOffset(24)]
		private readonly int _flags;
		public ChannelDescriptorFlags Flags => (ChannelDescriptorFlags)(byte)_flags;
		public int NumGlobalUnknowns => _flags >>> 28;
		[FieldOffset(28)]
		public readonly NativeMethods.HidParsingUnknownToken GlobalUnknown0;
		[FieldOffset(36)]
		public readonly NativeMethods.HidParsingUnknownToken GlobalUnknown1;
		[FieldOffset(44)]
		public readonly NativeMethods.HidParsingUnknownToken GlobalUnknown2;
		[FieldOffset(52)]
		public readonly NativeMethods.HidParsingUnknownToken GlobalUnknown3;
		[FieldOffset(60)]
		public readonly NativeMethods.RangeCaps Range;
		[FieldOffset(60)]
		public readonly NativeMethods.RangeCaps NotRange;
		[FieldOffset(76)]
		public readonly ButtonCaps Button;
		[FieldOffset(76)]
		public readonly DataCaps Data;
		[FieldOffset(96)]
		public readonly uint Units;
		[FieldOffset(100)]
		public readonly uint UnitExp;
	};

	[StructLayout(LayoutKind.Sequential)]
	private readonly struct ButtonCaps
	{
		public readonly int LogicalMin;
		public readonly int LogicalMax;
	}

	[StructLayout(LayoutKind.Sequential)]
	private readonly struct DataCaps
	{
		public readonly byte HasNull;
		public readonly byte Reserved0;
		public readonly byte Reserved1;
		public readonly byte Reserved2;
		public readonly byte Reserved3;
		public readonly int LogicalMin;
		public readonly int LogicalMax;
		public readonly int PhysicalMin;
		public readonly int PhysicalMax;
	}

	[Flags]
	private enum ChannelDescriptorFlags : byte
	{
		None = 0b00000000,
		MoreChannels = 0b00000001,
		IsConst = 0b00000010,
		IsButton = 0b00000100,
		IsAbsolute = 0b00001000,
		IsRange = 0b00010000,
		IsAlias = 0b00100000,
		IsStringRange = 0b01000000,
		IsDesignatorRange = 0b10000000,
	}

	private readonly byte[] _data;

	internal HidCollectionDescriptor(byte[] data)
	{
		ValidateDescriptor(data);
		_data = data;
	}

	private static void ValidateDescriptor(ReadOnlySpan<byte> data)
	{
		// We do some validation here in order to ensure data integrity.
		// Although we should receive descriptors from trusted source, it will allow to export this constructor publicly.
		if (data.Length < Unsafe.SizeOf<PreparsedDataHeader>() || !data.Slice(0, 8).SequenceEqual("HidP KDR"u8)) goto InvalidDescriptor;
		ref var header = ref Unsafe.As<byte, PreparsedDataHeader>(ref Unsafe.AsRef(data[0]));
		return;
	InvalidDescriptor:;
		throw new ArgumentException("Invalid descriptor.");
	}

	private ref PreparsedDataHeader Header => ref Unsafe.As<byte, PreparsedDataHeader>(ref _data[0]);

	internal byte[] Data => _data;

	public ushort Usage => Header.Usage;
	public HidUsagePage UsagePage => (HidUsagePage)Header.UsagePage;

	public ushort InputReportLength => Header.Input.ByteLen;
	public ushort OutputReportLength => Header.Output.ByteLen;
	public ushort FeatureReportLength => Header.Feature.ByteLen;
}
