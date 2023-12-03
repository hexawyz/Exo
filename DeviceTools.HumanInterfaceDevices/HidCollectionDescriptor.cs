using System.Collections;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace DeviceTools.HumanInterfaceDevices;

// I made the choice to erase the original preparsed data in favor of a reinterpreted C# model.
// The downside is that this will inevitably result in more allocations, but I couldn't find a good way to model this otherwise.
// The preparsed data could still be exposed as-is later if needed, as it is still used as the sourc eof truth, but considering this class structure will expose all the information,
// it shouldn't be needed at all. So, for the time being, raw preparsed data will be not be exposed anywhere.
// Also worth noting here, is that we use the reverse-engineered structure of the preparsed data instead of calling the HID API to query inside the data structure.
// While relatively unlikely, this could break in the future. However, a lot of code (such as Chromium?) relies on these internals, so we should be mostly safe.
// It would all have been much better and simpler if Windows just exposed the raw HID descriptors. (Although it wouldn't go well with splitting devices in multiple collections)
public sealed class HidCollectionDescriptor
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
		public HidChannelDescriptorFlags Flags => (HidChannelDescriptorFlags)(byte)_flags;
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
		public readonly NativeMethods.NotRangeCaps NotRange;
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

	private class ReportDescriptorBuilder
	{
		public ReportDescriptorBuilder(byte reportId) => ReportId = reportId;

		public List<HidChannelDescriptor> Channels { get; } = new();
		public ushort ReportLength { get; set; }
		public byte ReportId { get; }

		public HidReportDescriptor ToReportDescriptor() => new(ReportId, ReportLength, Channels.ToArray());
	}

	internal static HidCollectionDescriptor Parse(ReadOnlySpan<byte> preparsedData)
	{
		static ReadOnlySpan<ChannelDescriptor> GetChannels(ReadOnlySpan<byte> data, ref readonly ChannelReportHeader header)
			 => MemoryMarshal.Cast<byte, ChannelDescriptor>(data.Slice(Unsafe.SizeOf<PreparsedDataHeader>() + header.Offset * Unsafe.SizeOf<ChannelDescriptor>(), header.Size * Unsafe.SizeOf<ChannelDescriptor>()));

		static HidReportDescriptor[] ParseReports(ReadOnlySpan<byte> data, ref readonly ChannelReportHeader header)
		{
			var channels = GetChannels(data, in header);
			var reports = new List<ReportDescriptorBuilder>();

			ReportDescriptorBuilder? currentReport = null;
			foreach (var channel in channels)
			{
				byte reportId = channel.ReportId;

				if (currentReport is null || currentReport.ReportId != reportId)
				{
					currentReport = reports.Find(r => r.ReportId == reportId);
					if (currentReport is null)
					{
						currentReport = new(reportId);
						reports.Add(currentReport);
					}
				}

				currentReport.ReportLength = Math.Max(currentReport.ReportLength, channel.ByteEnd);
				currentReport.Channels.Add
				(
					(channel.Flags & HidChannelDescriptorFlags.IsButton) != 0 ?
					new HidButtonDescriptor
					(
						(HidUsagePage)channel.UsagePage,
						channel.ReportSize,
						channel.ReportCount,
						channel.ByteOffset,
						channel.ByteEnd,
						channel.Flags,
						channel.BitOffset,
						channel.BitLength,
						(channel.Flags & HidChannelDescriptorFlags.IsRange) != 0 ? new(channel.Range.UsageMin, channel.Range.UsageMax) : new(channel.NotRange.Usage),
						(channel.Flags & HidChannelDescriptorFlags.IsStringRange) != 0 ? new(channel.Range.StringMin, channel.Range.StringMax) : new(channel.NotRange.StringIndex),
						(channel.Flags & HidChannelDescriptorFlags.IsDesignatorRange) != 0 ? new(channel.Range.DesignatorMin, channel.Range.DesignatorMax) : new(channel.NotRange.DesignatorIndex),
						(channel.Flags & HidChannelDescriptorFlags.IsRange) != 0 ? new(channel.Range.DataIndexMin, channel.Range.DataIndexMax) : new(channel.NotRange.DataIndex),
						new(channel.Button.LogicalMin, channel.Button.LogicalMax)
					) :
					new HidValueDescriptor
					(
						(HidUsagePage)channel.UsagePage,
						channel.ReportSize,
						channel.ReportCount,
						channel.ByteOffset,
						channel.ByteEnd,
						channel.Flags,
						channel.BitOffset,
						channel.BitLength,
						(channel.Flags & HidChannelDescriptorFlags.IsRange) != 0 ? new(channel.Range.UsageMin, channel.Range.UsageMax) : new(channel.NotRange.Usage),
						(channel.Flags & HidChannelDescriptorFlags.IsStringRange) != 0 ? new(channel.Range.StringMin, channel.Range.StringMax) : new(channel.NotRange.StringIndex),
						(channel.Flags & HidChannelDescriptorFlags.IsDesignatorRange) != 0 ? new(channel.Range.DesignatorMin, channel.Range.DesignatorMax) : new(channel.NotRange.DesignatorIndex),
						(channel.Flags & HidChannelDescriptorFlags.IsRange) != 0 ? new(channel.Range.DataIndexMin, channel.Range.DataIndexMax) : new(channel.NotRange.DataIndex),
						new(channel.Data.LogicalMin, channel.Data.LogicalMax),
						new(channel.Data.PhysicalMin, channel.Data.PhysicalMax),
						channel.Data.HasNull != 0
					)
				);
			}

			var finalReports = new HidReportDescriptor[reports.Count];
			for (int i = 0; i < reports.Count; i++)
			{
				finalReports[i] = reports[i].ToReportDescriptor();
			}

			return finalReports;
		}

		// We do some validation here in order to ensure data integrity.
		// Although we should receive descriptors from trusted source, it will allow to export this constructor publicly.
		if (preparsedData.Length < Unsafe.SizeOf<PreparsedDataHeader>() || !preparsedData.Slice(0, 8).SequenceEqual("HidP KDR"u8)) goto InvalidDescriptor;
		ref var header = ref Unsafe.As<byte, PreparsedDataHeader>(ref Unsafe.AsRef(in preparsedData[0]));

		var inputReports = header.Input.Size > 0 ? new(ParseReports(preparsedData, in header.Input), header.Input.ByteLen) : HidInputReportDescriptorCollection.Empty;
		var outputReports = header.Input.Size > 0 ? new(ParseReports(preparsedData, in header.Input), header.Input.ByteLen) : HidOutputReportDescriptorCollection.Empty;
		var featureReports = header.Input.Size > 0 ? new(ParseReports(preparsedData, in header.Input), header.Input.ByteLen) : HidFeatureReportDescriptorCollection.Empty;

		return new(header.Usage, (HidUsagePage)header.UsagePage, (SystemButtons)header.PowerButtonMask, inputReports, outputReports, featureReports);
	InvalidDescriptor:;
		throw new ArgumentException("Invalid descriptor.");
	}

	public HidCollectionDescriptor
	(
		ushort usage,
		HidUsagePage usagePage,
		SystemButtons powerButtons,
		HidInputReportDescriptorCollection inputReports,
		HidOutputReportDescriptorCollection outputReports,
		HidFeatureReportDescriptorCollection featureReports
	)
	{
		Usage = usage;
		UsagePage = usagePage;
		PowerButtons = powerButtons;
		InputReports = inputReports;
		OutputReports = outputReports;
		FeatureReports = featureReports;
	}

	public ushort Usage { get; }
	public HidUsagePage UsagePage { get; }
	public SystemButtons PowerButtons { get; }

	public HidInputReportDescriptorCollection InputReports { get; }
	public HidOutputReportDescriptorCollection OutputReports { get; }
	public HidFeatureReportDescriptorCollection FeatureReports { get; }
}

[Flags]
internal enum HidChannelDescriptorFlags : byte
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

public abstract class HidReportDescriptorCollection : IReadOnlyList<HidReportDescriptor>, IList<HidReportDescriptor>
{
	public struct Enumerator : IEnumerator<HidReportDescriptor>
	{
		private readonly HidReportDescriptor[] _reports;
		private int _index;

		[EditorBrowsable(EditorBrowsableState.Never)]
		public Enumerator() => throw new InvalidOperationException();

		internal Enumerator(HidReportDescriptor[] reports)
		{
			_reports = reports;
			_index = -1;
		}

		public readonly void Dispose() { }

		public readonly HidReportDescriptor Current => _reports[_index];
		readonly object IEnumerator.Current => Current;

		public bool MoveNext() => ++_index < _reports.Length;
		public void Reset() => _index = -1;
	}

	private readonly HidReportDescriptor[] _reports;

	private protected HidReportDescriptorCollection(HidReportDescriptor[] reports, int maximumReportLength)
	{
		_reports = reports;
		MaximumReportLength = maximumReportLength;
	}

	public abstract HidReportType ReportType { get; }
	public int MaximumReportLength { get; }

	public HidReportDescriptor this[int index] => _reports[index];
	HidReportDescriptor IList<HidReportDescriptor>.this[int index]
	{
		get => _reports[index];
		set => throw new NotSupportedException();
	}

	public int Count => _reports.Length;

	bool ICollection<HidReportDescriptor>.IsReadOnly => true;

	public Enumerator GetEnumerator() => new Enumerator(_reports);
	IEnumerator<HidReportDescriptor> IEnumerable<HidReportDescriptor>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

	public int IndexOf(HidReportDescriptor item) => Array.IndexOf(_reports, item);
	public bool Contains(HidReportDescriptor item) => Array.IndexOf(_reports, item) >= 0;
	public void CopyTo(HidReportDescriptor[] array, int arrayIndex) => Array.Copy(_reports, 0, array, arrayIndex, _reports.Length);

	void ICollection<HidReportDescriptor>.Add(HidReportDescriptor item) => throw new NotSupportedException();
	void IList<HidReportDescriptor>.Insert(int index, HidReportDescriptor item) => throw new NotSupportedException();

	bool ICollection<HidReportDescriptor>.Remove(HidReportDescriptor item) => throw new NotSupportedException();
	void IList<HidReportDescriptor>.RemoveAt(int index) => throw new NotSupportedException();

	void ICollection<HidReportDescriptor>.Clear() => throw new NotSupportedException();
}

public sealed class HidInputReportDescriptorCollection : HidReportDescriptorCollection
{
	internal static readonly HidInputReportDescriptorCollection Empty = new(Array.Empty<HidReportDescriptor>(), 0);

	internal HidInputReportDescriptorCollection(HidReportDescriptor[] reports, int maximumReportLength)
		: base(reports, maximumReportLength)
	{
	}

	public override HidReportType ReportType => HidReportType.Input;
}

public sealed class HidOutputReportDescriptorCollection : HidReportDescriptorCollection
{
	internal static readonly HidOutputReportDescriptorCollection Empty = new(Array.Empty<HidReportDescriptor>(), 0);

	internal HidOutputReportDescriptorCollection(HidReportDescriptor[] reports, int maximumReportLength)
		: base(reports, maximumReportLength)
	{
	}

	public override HidReportType ReportType => HidReportType.Output;
}

public sealed class HidFeatureReportDescriptorCollection : HidReportDescriptorCollection
{
	internal static readonly HidFeatureReportDescriptorCollection Empty = new(Array.Empty<HidReportDescriptor>(), 0);

	internal HidFeatureReportDescriptorCollection(HidReportDescriptor[] reports, int maximumReportLength)
		: base(reports, maximumReportLength)
	{
	}

	public override HidReportType ReportType => HidReportType.Feature;
}

public sealed class HidReportDescriptor
{
	public HidReportDescriptor(byte reportId, ushort reportSize, IReadOnlyList<HidChannelDescriptor> channels)
	{
		ReportId = reportId;
		ReportSize = reportSize;
		Channels = channels;
	}

	public byte ReportId { get; }
	public ushort ReportSize { get; }
	public IReadOnlyList<HidChannelDescriptor> Channels { get; }
}

public readonly struct HidValueRange<T>
#if NET7_0_OR_GREATER
	where T : unmanaged, IComparable<T>, INumber<T>
#else
	where T : unmanaged, IComparable<T>
#endif
{
	public HidValueRange(T value) : this(value, value) { }

	public HidValueRange(T minimum, T maximum)
	{
		Minimum = minimum;
		Maximum = maximum;
	}

	public T Minimum { get; }
	public T Maximum { get; }
}

public abstract class HidChannelDescriptor
{
	private protected HidChannelDescriptor
	(
		HidUsagePage usagePage,
		ushort itemBitLength,
		ushort itemCount,
		ushort sequenceStartByteIndex,
		ushort sequenceEndByteIndex,
		HidChannelDescriptorFlags flags,
		byte sequenceBitOffset,
		ushort sequenceBitLength,
		HidValueRange<ushort> usageRange,
		HidValueRange<ushort> stringRange,
		HidValueRange<ushort> designatorRange,
		HidValueRange<ushort> dataIndexRange,
		HidValueRange<int> logicalRange
	)
	{
		UsagePage = usagePage;
		ItemBitLength = itemBitLength;
		ItemCount = itemCount;
		SequenceByteIndex = sequenceStartByteIndex;
		SequenceByteLength = (ushort)(sequenceEndByteIndex - sequenceStartByteIndex);
		Flags = flags;
		SequenceBitOffset = sequenceBitOffset;
		SequenceBitLength = sequenceBitLength;
		UsageRange = usageRange;
		StringRange = stringRange;
		DesignatorRange = designatorRange;
		DataIndexRange = dataIndexRange;
		LogicalRange = logicalRange;
	}

	public HidUsagePage UsagePage { get; }

	public ushort ItemBitLength { get; }
	public ushort ItemCount { get; }

	public ushort SequenceByteIndex { get; }
	public ushort SequenceByteLength { get; }

	internal HidChannelDescriptorFlags Flags { get; }
	public byte SequenceBitOffset { get; }
	public ushort SequenceBitLength { get; }

	public HidValueRange<ushort> UsageRange { get; }
	public HidValueRange<ushort> StringRange { get; }
	public HidValueRange<ushort> DesignatorRange { get; }
	public HidValueRange<ushort> DataIndexRange { get; }
	public HidValueRange<int> LogicalRange { get; }

	public bool HasMoreChannels => (Flags & HidChannelDescriptorFlags.MoreChannels) != 0;
	public bool IsConstant => (Flags & HidChannelDescriptorFlags.IsConst) != 0;
	public bool IsAbsolute => (Flags & HidChannelDescriptorFlags.IsAbsolute) != 0;
	public bool IsRange => (Flags & HidChannelDescriptorFlags.IsRange) != 0;
	public bool IsAlias => (Flags & HidChannelDescriptorFlags.IsAlias) != 0;
	public bool IsStringRange => (Flags & HidChannelDescriptorFlags.IsStringRange) != 0;
	public bool IsDesignatorRange => (Flags & HidChannelDescriptorFlags.IsDesignatorRange) != 0;
}

public sealed class HidButtonDescriptor : HidChannelDescriptor
{
	internal HidButtonDescriptor
	(
		HidUsagePage usagePage,
		ushort itemBitLength,
		ushort itemCount,
		ushort sequenceStartByteIndex,
		ushort sequenceEndByteIndex,
		HidChannelDescriptorFlags flags,
		byte sequenceBitOffset,
		ushort sequenceBitLength,
		HidValueRange<ushort> usageRange,
		HidValueRange<ushort> stringRange,
		HidValueRange<ushort> designatorRange,
		HidValueRange<ushort> dataIndexRange,
		HidValueRange<int> logicalRange
	) : base
		(
			usagePage,
			itemBitLength,
			itemCount,
			sequenceStartByteIndex,
			sequenceEndByteIndex,
			flags,
			sequenceBitOffset,
			sequenceBitLength,
			usageRange,
			stringRange,
			designatorRange,
			dataIndexRange,
			logicalRange
		)
	{
	}
}

public sealed class HidValueDescriptor : HidChannelDescriptor
{
	internal HidValueDescriptor
	(
		HidUsagePage usagePage,
		ushort itemBitLength,
		ushort itemCount,
		ushort sequenceStartByteIndex,
		ushort sequenceEndByteIndex,
		HidChannelDescriptorFlags flags,
		byte sequenceBitOffset,
		ushort sequenceBitLength,
		HidValueRange<ushort> usageRange,
		HidValueRange<ushort> stringRange,
		HidValueRange<ushort> designatorRange,
		HidValueRange<ushort> dataIndexRange,
		HidValueRange<int> logicalRange,
		HidValueRange<int> physicalRange,
		bool hasNullValue
	) : base
		(
			usagePage,
			itemBitLength,
			itemCount,
			sequenceStartByteIndex,
			sequenceEndByteIndex,
			flags,
			sequenceBitOffset,
			sequenceBitLength,
			usageRange,
			stringRange,
			designatorRange,
			dataIndexRange,
			logicalRange
		)
	{
		PhysicalRange = physicalRange;
		HasNullValue = hasNullValue;
	}

	public HidValueRange<int> PhysicalRange { get; }
	public bool HasNullValue { get; }
}

[Flags]
public enum SystemButtons
{
	Power = 0x00000001,
	Sleep = 0x00000002,
	Lid = 0x00000004,
	LidStateMask = 0x00030000,
	LidOpen = 0x00010000,
	LidClosed = 0x00020000,
	LidInitial = 0x00040000,
	LidChanged = 0x00080000,
	Wake = int.MinValue,
}
