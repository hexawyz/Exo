using System.Buffers.Binary;
using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
		// This is declared as a C++ bit field. We cannot split it into bytes if we want the code to be compatible with big endian.
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
		public readonly int UnitExp;
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

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct LinkCollectionNode
	{
		public readonly ushort LinkUsage;
		public readonly HidUsagePage LinkUsagePage;
		public readonly ushort Parent;
		public readonly ushort ChildCount;
		public readonly ushort NextSibling;
		public readonly ushort FirstChild;

		// This is declared as a C++ bit field. We cannot split it into bytes if we want the code to be compatible with big endian.
		private readonly uint _packedBitFieldData;

		public HidCollectionType CollectionType => (HidCollectionType)(byte)_packedBitFieldData;

		public bool IsAlias => (_packedBitFieldData & 0x100) != 0;
	}

	private class ReportDescriptorBuilder
	{
		public ReportDescriptorBuilder(byte reportId) => ReportId = reportId;

		public List<HidChannelDescriptor> Channels { get; } = new();
		public ushort ReportLength { get; set; }
		public byte ReportId { get; }

		public HidReportDescriptor ToReportDescriptor() => new(ReportId, ReportLength, Channels.ToArray());
	}

	internal static ImmutableArray<T> AsImmutable<T>(T[] array) => Unsafe.As<T[], ImmutableArray<T>>(ref array);

	internal static T[] AsMutable<T>(ImmutableArray<T> array) => Unsafe.As<ImmutableArray<T>, T[]>(ref array);

	internal static HidCollectionDescriptor Parse(ReadOnlySpan<byte> preparsedData)
	{
		static ReadOnlySpan<ChannelDescriptor> GetChannels(ReadOnlySpan<byte> data, ref readonly ChannelReportHeader header)
			 => MemoryMarshal.Cast<byte, ChannelDescriptor>(data.Slice(Unsafe.SizeOf<PreparsedDataHeader>() + header.Offset * Unsafe.SizeOf<ChannelDescriptor>(), header.Size * Unsafe.SizeOf<ChannelDescriptor>()));

		static ReadOnlySpan<LinkCollectionNode> GetLinkNodes(ReadOnlySpan<byte> data)
		{
			ref var header = ref Unsafe.As<byte, PreparsedDataHeader>(ref Unsafe.AsRef(in data[0]));
			return MemoryMarshal.Cast<byte, LinkCollectionNode>(data.Slice(Unsafe.SizeOf<PreparsedDataHeader>() + header.LinkCollectionArrayOffset, header.LinkCollectionArrayLength * Unsafe.SizeOf<LinkCollectionNode>()));
		}

		// Collection node processing is done in multiple passes for simplicity.
		static HidLinkCollection[] ParseLinkCollections(ReadOnlySpan<byte> data)
		{
			var nodes = GetLinkNodes(data);
			// During the first pass, we'll create collection nodes with children not filled in.
			// This is possible because the arrays will be stored as-is and will still be writable after object creation. (Because we control the model)
			// Hopefully, the nodes are laid out in logical order with parents before children, but just in case it is not the case, we'll support multiple sub-passes to create all the nodes.
			var collections = new HidLinkCollection?[nodes.Length];
			int initializedNodes = 0;
			do
			{
				int firstAliasIndex = -1;
				for (int i = 0; i < nodes.Length; i++)
				{
					if (collections[i] is not null) continue;

					var node = nodes[i];
					var parent = collections[node.Parent];
					if (parent is null && i > 0) continue;

					// All aliased nodes are supposed to appear sequentially in the array, with the main node appearing last without the IsAlias flag.
					// NB: All aliased nodes should have the same parent, so there shouldn't be any weird interaction with the null parent skipping logic below.
					if (node.IsAlias)
					{
						if (parent is null) throw new InvalidOperationException("Parent node cannot be aliased.");
						if (firstAliasIndex < 0) firstAliasIndex = i;
						continue;
					}

					var children = AsImmutable(node.ChildCount > 0 ? new HidLinkCollection[node.ChildCount] : Array.Empty<HidLinkCollection>());

					var collection = firstAliasIndex >= 0 ?
						HidLinkCollection.CreateAliased(node.LinkUsagePage, node.LinkUsage, parent!, children) :
						HidLinkCollection.Create(node.LinkUsagePage, node.LinkUsage, parent, children);
					collections[i] = collection;
					initializedNodes++;

					if (firstAliasIndex >= 0)
					{
						// Create all the (previous) aliased nodes now.
						// We're relying on the fact that the parent of aliased nodes should be the same. (Hopefully, otherwise it wouldn't even make sense)
						for (int j = firstAliasIndex; j < i; j++)
						{
							node = nodes[j];
							children = AsImmutable(node.ChildCount > 0 ? new HidLinkCollection[node.ChildCount] : Array.Empty<HidLinkCollection>());
							collections[j] = HidLinkCollection.CreateAliased(node.LinkUsagePage, node.LinkUsage, parent!, collection, children);
							initializedNodes++;
						}

						firstAliasIndex = -1;
					}
				}
			}
			while (initializedNodes < nodes.Length);

			// During the second pass, we'll define the children for all nodes.
			for (int i = 0; i < collections.Length; i++)
			{
				var children = AsMutable(collections[i]!.Children);

				if (children.Length == 0) continue;

				int childIndex = nodes[i].FirstChild;
				int childCount = 0;
				while (childIndex > 0)
				{
					var child = collections[childIndex]!;
					children[childCount++] = child;
					childIndex = nodes[childIndex].NextSibling;
				}
				if (childCount != children.Length) throw new InvalidOperationException("The number of child nodes does not match.");
			}

			return collections!;
		}

		static HidReportDescriptor[] ParseReports(HidLinkCollection[] linkCollections, ReadOnlySpan<byte> data, ref readonly ChannelReportHeader header)
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
						linkCollections[channel.LinkCollection],
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
						HidUnit.FromRawValue(channel.Units, channel.UnitExp),
						new(channel.Button.LogicalMin, channel.Button.LogicalMax)
					) :
					new HidValueDescriptor
					(
						linkCollections[channel.LinkCollection],
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
						HidUnit.FromRawValue(channel.Units, channel.UnitExp),
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

		var linkCollections = ParseLinkCollections(preparsedData);

		var inputReports = header.Input.Size > 0 ? new(ParseReports(linkCollections, preparsedData, in header.Input), header.Input.ByteLen) : HidInputReportDescriptorCollection.Empty;
		var outputReports = header.Output.Size > 0 ? new(ParseReports(linkCollections, preparsedData, in header.Output), header.Output.ByteLen) : HidOutputReportDescriptorCollection.Empty;
		var featureReports = header.Feature.Size > 0 ? new(ParseReports(linkCollections, preparsedData, in header.Feature), header.Feature.ByteLen) : HidFeatureReportDescriptorCollection.Empty;

		return new(linkCollections[0], header.Usage, (HidUsagePage)header.UsagePage, (SystemButtons)header.PowerButtonMask, inputReports, outputReports, featureReports);
	InvalidDescriptor:;
		throw new ArgumentException("Invalid descriptor.");
	}

	private HidCollectionDescriptor
	(
		HidLinkCollection linkCollection,
		ushort usage,
		HidUsagePage usagePage,
		SystemButtons powerButtons,
		HidInputReportDescriptorCollection inputReports,
		HidOutputReportDescriptorCollection outputReports,
		HidFeatureReportDescriptorCollection featureReports
	)
	{
		LinkCollection = linkCollection;
		Usage = usage;
		UsagePage = usagePage;
		PowerButtons = powerButtons;
		InputReports = inputReports;
		OutputReports = outputReports;
		FeatureReports = featureReports;
	}

	/// <summary>Gets the root node of the link collection graph.</summary>
	public HidLinkCollection LinkCollection { get; }

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
		HidLinkCollection linkCollection,
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
		HidUnit unit,
		HidValueRange<int> logicalRange
	)
	{
		LinkCollection = linkCollection;
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
		Unit = unit;
		LogicalRange = logicalRange;
	}

	public HidLinkCollection LinkCollection { get; }

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
	public HidUnit Unit { get; }
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
		HidLinkCollection linkCollection,
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
		HidUnit unit,
		HidValueRange<int> logicalRange
	) : base
		(
			linkCollection,
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
			unit,
			logicalRange
		)
	{
	}
}

public sealed class HidValueDescriptor : HidChannelDescriptor
{
	internal HidValueDescriptor
	(
		HidLinkCollection linkCollection,
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
		HidUnit unit,
		HidValueRange<int> logicalRange,
		HidValueRange<int> physicalRange,
		bool hasNullValue
	) : base
		(
			linkCollection,
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
			unit,
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

public class HidLinkCollection
{
	internal static HidLinkCollection Create(HidUsagePage usagePage, ushort usage, HidLinkCollection? parent, ImmutableArray<HidLinkCollection> children)
		=> new(usagePage, usage, parent, null, children);

	internal static HidLinkCollection CreateAliased(HidUsagePage usagePage, ushort usage, HidLinkCollection parent, ImmutableArray<HidLinkCollection> children)
		=> new(usagePage, usage, parent, children);

	internal static HidLinkCollection CreateAliased(HidUsagePage usagePage, ushort usage, HidLinkCollection parent, HidLinkCollection aliasedCollection, ImmutableArray<HidLinkCollection> children)
		=> new(usagePage, usage, parent, aliasedCollection, children);

	private HidLinkCollection(HidUsagePage usagePage, ushort usage, HidLinkCollection? parent, ImmutableArray<HidLinkCollection> children)
	{
		UsagePage = usagePage;
		Usage = usage;
		Parent = parent;
		AliasedCollection = this;
		Children = children;
	}

	private HidLinkCollection(HidUsagePage usagePage, ushort usage, HidLinkCollection? parent, HidLinkCollection? aliasedCollection, ImmutableArray<HidLinkCollection> children)
	{
		UsagePage = usagePage;
		Usage = usage;
		Parent = parent;
		AliasedCollection = aliasedCollection;
		Children = children;
	}

	public HidLinkCollection? Parent { get; }

	/// <summary>If the collection is aliased, gets the main collection against which it is aliased, or itself if it is the main one.</summary>
	public HidLinkCollection? AliasedCollection { get; }

	public ImmutableArray<HidLinkCollection> Children { get; }

	public HidUsagePage UsagePage { get; }
	public ushort Usage { get; }
}

public enum HidUnitSystem : byte
{
	None = 0,
	SiLinear = 1,
	SiRotation = 2,
	EnglishLinear = 3,
	EnglishRotation = 4,
}

// We don't strictly need to declare this as having fixed layout, but having the raw value as a field should ensure proper alignment.
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct HidUnit : IEquatable<HidUnit>
{
	private const string Exponents = "⁰¹²³⁴⁵⁶⁷⁸⁹";
	private static readonly string[][] SystemUnits =
	[
		[ "cm", "g", "s", "K", "A", "cd"],
		[ "rad", "g", "s", "K", "A", "cd"],
		[ "in", "slug", "s", "°F", "A", "cd"],
		[ "°", "slug", "s", "°F", "A", "cd"],
	];

	public static HidUnit Centimeters => FromRawValue(0x0_0_0_0_0_0_1_1, 0);
	public static HidUnit Inches => FromRawValue(0x0_0_0_0_0_0_1_3, 0);
	public static HidUnit Radians => FromRawValue(0x0_0_0_0_0_0_1_2, 0);
	public static HidUnit Degrees => FromRawValue(0x0_0_0_0_0_0_1_4, 0);
	public static HidUnit Meters => FromRawValue(0x0_0_0_0_0_0_1_1, 2);
	public static HidUnit Kilometers => FromRawValue(0x0_0_0_0_0_0_1_1, 5);
	public static HidUnit Grams => FromRawValue(0x0_0_0_0_0_1_0_1, 0);
	public static HidUnit Kilograms => FromRawValue(0x0_0_0_0_0_1_0_1, 3);
	public static HidUnit Seconds => FromRawValue(0x0_0_0_0_1_0_0_1, 0);
	public static HidUnit Kelvins => FromRawValue(0x0_0_0_1_0_0_0_1, 0);
	public static HidUnit Fahrenheits => FromRawValue(0x0_0_0_1_0_0_0_3, 0);
	public static HidUnit CentimetersPerSecond => FromRawValue(0x0_0_0_0_F_0_1_1, 0);
	public static HidUnit MetersPerSecond => FromRawValue(0x0_0_0_0_F_0_1_1, 2);
	public static HidUnit KilometersPerSecond => FromRawValue(0x0_0_0_0_F_0_1_1, 5);
	public static HidUnit Joules => FromRawValue(0x0_0_0_0_E_1_2_1, 7);

	// We split the 32 bit value into multiple parts here, because it is accessed by 4bit blocks anyway.
	[FieldOffset(0)]
	private readonly sbyte _value0;
	[FieldOffset(1)]
	private readonly sbyte _value1;
	[FieldOffset(2)]
	private readonly sbyte _value2;
	[FieldOffset(3)]
	private readonly sbyte _value3;
	[FieldOffset(4)]
	private readonly int _exponent;

	[FieldOffset(0)]
	private readonly uint _unit;
	[FieldOffset(0)]
	private readonly ulong _rawValue;

	public HidUnitSystem System => (HidUnitSystem)(_value0 & 0xF);
	public sbyte LengthExponent => (sbyte)(_value0 >> 4);
	public sbyte MassExponent => (sbyte)((sbyte)(_value1 << 4) >> 4);
	public sbyte TimeExponent => (sbyte)(_value1 >> 4);
	public sbyte TemperatureExponent => (sbyte)((sbyte)(_value2 << 4) >> 4);
	public sbyte CurrentExponent => (sbyte)(_value2 >> 4);
	public sbyte LuminousIntensityExponent => (sbyte)((sbyte)(_value3 << 4) >> 4);
	public int Exponent => _exponent;

	public bool IsDefault => _rawValue == 0;

	public static HidUnit FromRawValue(uint unit, int exponent)
		=> FromRawValue(BitConverter.IsLittleEndian ? unit | (ulong)(uint)exponent << 32 : (ulong)BinaryPrimitives.ReverseEndianness(unit) << 32 | (uint)exponent);

	private static HidUnit FromRawValue(ulong rawValue)
		=> Unsafe.As<ulong, HidUnit>(ref rawValue);

	public override string ToString()
	{
		if (_rawValue == 0) return "counts";
		var system = (int)System - 1;
		if ((uint)system >= SystemUnits.Length) return "unknown";
		if ((_rawValue & ~0xFU) == 0) return "counts";
		var units = SystemUnits[system];
		// The maximum possible length of the string should be 43 characters.
		// => 12 characters for unit names, 6 multiplicative dots, 12 character for unit exponents, 2 characters for ten, up to 11 characters for power of ten.
		Span<char> buffer = stackalloc char[43];
		int offset = 0;
		int unitIndex = 0;
		if (_exponent != 0)
		{
			"10".AsSpan().CopyTo(buffer.Slice(offset));
			offset += 2;
			if (_exponent != 1)
			{
				// Use the standard .NET APIs to format the number
#if NETSTANDARD2_0
				string exponentString = _exponent.ToString(CultureInfo.InvariantCulture);
				exponentString.AsSpan().CopyTo(buffer.Slice(offset));
				int count = exponentString.Length;
#else
				_exponent.TryFormat(buffer[offset..], out int count, default, CultureInfo.InvariantCulture);
#endif
				// Post-process the formatted number to have it as an exponent.
				if (_exponent < 0)
				{
					buffer[offset++] = '⁻';
					count--;
				}
				for (int i = 0; i < count; i++)
				{
					ref char ch = ref buffer[offset + i];
					ch = Exponents[ch - '0'];
				}
				offset += count;
			}
		}
		uint remainingUnits = BitConverter.IsLittleEndian ? _unit : BinaryPrimitives.ReverseEndianness(_unit);
		while (remainingUnits > 0xF && unitIndex < SystemUnits.Length)
		{
			sbyte exponent = (sbyte)((sbyte)remainingUnits >> 4);
			string unit = units[unitIndex++];
			if (exponent != 0)
			{
				if (offset > 0) buffer[offset++] = '⋅';
				unit.AsSpan().CopyTo(buffer.Slice(offset));
				offset += unit.Length;
				if (exponent != 1)
				{
					if (exponent < 0) buffer[offset++] = '⁻';
					buffer[offset++] = Exponents[Math.Abs(exponent)];
				}
			}
			remainingUnits >>= 4;
		}
		return buffer.Slice(0, offset).ToString();
	}

	public override bool Equals(object? obj) => obj is HidUnit unit && Equals(unit);
	public bool Equals(HidUnit other) => _rawValue == other._rawValue;
	public override int GetHashCode() => _rawValue.GetHashCode();

	public static bool operator ==(HidUnit left, HidUnit right) => left.Equals(right);
	public static bool operator !=(HidUnit left, HidUnit right) => !(left == right);
}
