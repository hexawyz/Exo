using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AnyLayout.SourceGenerators
{
	[Generator]
	public sealed class UsbIdDatabaseGenerator : ISourceGenerator
	{
		public void Execute(SourceGeneratorContext context)
		{
			if (!(context.AdditionalFiles.Where(at => Path.GetFileName(at.Path) == "usb.ids").SingleOrDefault()?.GetText() is SourceText text))
			{
				return;
			}

			var vendors = new List<(ushort VendorId, string VendorName, List<(ushort ProductId, string ProductName)> Products)>();
			var currentProducts = null as List<(ushort ProductId, string ProductName)>;
			foreach (var line in text.Lines)
			{
				string lineText = line.ToString();

				if (lineText == "# List of known device classes, subclasses and protocols") break;

				if (lineText is { Length: 0 } || lineText[0] == '#') continue;

				if (Regex.Match(lineText, @"(?<Id>[0-9a-fA-F]{4})  (?<Name>.*)$", RegexOptions.ExplicitCapture) is Match match && match.Success)
				{
					var id = ushort.Parse(match.Groups["Id"].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
					var name = match.Groups["Name"].Value;
					if (lineText[0] == '\t')
					{
						if (currentProducts is null) throw new Exception();
						currentProducts.Add((id, name));
					}
					else
					{
						currentProducts = new List<(ushort ProductId, string Name)>();
						vendors.Add((id, name, currentProducts));
					}
				}
				else
				{
					throw new Exception();
				}

			}

			var stringMap =
			(
				from v in vendors
				from name in
				(
					from p in v.Products
					select p.ProductName
				).Prepend(v.VendorName)
				select name
			)
				.Distinct()
				.ToDictionary(s => s, s => new StringInfo(s));

			var strings = stringMap.Values.ToArray();

			// Sort the texts by length to ease the process of reducing string data.
			Array.Sort(strings, (a, b) => Comparer<int>.Default.Compare(a.Text.Length, b.Text.Length));

			// Split the texts into length buckets.
			int[] bucketEndIndices;
			{
				var bucketStartIndices = new List<int>();
				int currentBucketValue = int.MinValue;
				for (int i = 0; i < strings.Length; i++)
				{
					int length = strings[i].Text.Length;

					if (length > currentBucketValue)
					{
						currentBucketValue = length;
						bucketStartIndices.Add(i);
					}
				}
				bucketStartIndices.Remove(0);
				bucketEndIndices = bucketStartIndices.ToArray();
			}

			// Identify strings contained in other strings.
			for (int smallerStringIndex = 0, bucketIndex = 0; bucketIndex < bucketEndIndices.Length; bucketIndex++)
			{
				int bucketEndIndex = bucketEndIndices[bucketIndex];
				for (; smallerStringIndex < bucketEndIndex; smallerStringIndex++)
				{
					var smallerString = strings[smallerStringIndex];
					// Take the first match of string indices
					for (int largerStringIndex = bucketEndIndex; largerStringIndex < strings.Length; largerStringIndex++)
					{
						if (strings[largerStringIndex] is var largerString && largerString.Text.IndexOf(smallerString.Text, StringComparison.Ordinal) >= 0)
						{
							smallerString.ContainingString = largerString.Text;
							break;
						}
					}
				}
			}

			// Flatten the hierarchy of strings
			int changeCount;
			do
			{
				changeCount = 0;
				foreach (var info in strings)
				{
					string? finalContainer = null;
					var item = info;
					while (item.ContainingString is string container)
					{
						finalContainer = container;
						item = stringMap[container];
					}

					if (info.ContainingString != finalContainer)
					{
						info.ContainingString = finalContainer;
						changeCount++;
					}
				}
			} while (changeCount > 0);

			int currentIndex = 0;
			foreach (var info in stringMap.Values)
			{
				if (info.ContainingString is not null) continue;
				info.Index = currentIndex;
				currentIndex += info.LengthInUtf8;
			}

			int totalStringLength = 0;
			foreach (var info in stringMap.Values)
			{
				if (info.ContainingString is null)
				{
					totalStringLength += info.LengthInUtf8;
				}
				else
				{
					info.Index = stringMap[info.ContainingString].Index + info.ContainingString.IndexOf(info.Text, StringComparison.Ordinal);
				}
			}

			// Fill string data
			var stringData = new byte[totalStringLength];
			foreach (var info in strings)
			{
				if (info.ContainingString is not null) continue;

				Encoding.UTF8.GetBytes(info.Text, 0, info.Text.Length, stringData, info.Index.GetValueOrDefault());
			}

			var deviceCount = vendors.Sum(v => v.Products.Count);

			var deviceIndexData = new byte[vendors.Count * Unsafe.SizeOf<OffsetAndShortLength>() + deviceCount * Unsafe.SizeOf<DeviceIndexEntry>()];
			var vendorIndexData = new VendorIndexEntry[vendors.Count];

			// Fill index data.
			{
				int currentDeviceCount = 0;
				var unfilledData = deviceIndexData.AsSpan();

				for (int i = 0; i < vendors.Count; i++)
				{
					var (vendorId, vendorName, products) = vendors[i];

					MemoryMarshal.Cast<byte, OffsetAndShortLength>(unfilledData.Slice(0, Unsafe.SizeOf<OffsetAndShortLength>()))[0] = GetStringReference(stringMap, vendorName);
					unfilledData = unfilledData.Slice(Unsafe.SizeOf<OffsetAndShortLength>());
					int deviceByteCount = products.Count * Unsafe.SizeOf<DeviceIndexEntry>();
					var devices = MemoryMarshal.Cast<byte, DeviceIndexEntry>(unfilledData.Slice(0, deviceByteCount));
					for (int j = 0; j < products.Count; j++)
					{
						var (productId, productName) = products[j];
						devices[j] = new DeviceIndexEntry
						{
							Id = productId,
							NameReference = GetStringReference(stringMap, productName)
						};
					}
					unfilledData = unfilledData.Slice(deviceByteCount);

					vendorIndexData[i] = new VendorIndexEntry
					{
						Id = vendorId,
						TotalDeviceCount = checked((ushort)unchecked(currentDeviceCount += products.Count))
					};
				}
			}

			var sb = new StringBuilder();

			void AddFile(string spanName, ReadOnlySpan<byte> span, string visibility = "private")
			{
				sb!.Length = 0;
				AppendHeader(sb);
				AppendSpan(sb, spanName, span, "\t\t", visibility);
				AppendFooter(sb);
				context.AddSource("UsbProductNameDatabase.Generated." + spanName, SourceText.From(sb.ToString(), Encoding.UTF8));
			}

			AddFile("VendorIndexData", MemoryMarshal.Cast<VendorIndexEntry, byte>(vendorIndexData));
			AddFile("ProductIndexData", deviceIndexData);
			AddFile("StringData", stringData, "internal");
		}

		public void Initialize(InitializationContext context)
		{
		}

		private static void AppendHeader(StringBuilder sb)
		{
			sb.AppendLine("using System;")
				.AppendLine()
				.AppendLine("namespace AnyLayout")
				.AppendLine("{")
				.AppendLine("\tpartial class UsbProductNameDatabase")
				.AppendLine("\t{");
		}

		private static void AppendFooter(StringBuilder sb)
		{
			sb.AppendLine("\t}")
				.AppendLine("}");
		}

		private static OffsetAndShortLength GetStringReference(Dictionary<string, StringInfo> map, string text)
		{
			var stringInfo = map[text];
			return new OffsetAndShortLength
			{
				DataIndex = stringInfo.Index.GetValueOrDefault(),
				DataLength = checked((byte)stringInfo.LengthInUtf8),
			};
		}

		private static void AppendSpan(StringBuilder sb, string name, ReadOnlySpan<byte> data, string indent, string visibility = "private")
		{
			sb.Append(indent)
				.Append(visibility)
				.Append(" static ReadOnlySpan<byte> ")
				.Append(name)
				.AppendLine(" => new byte[]")
				.Append(indent)
				.AppendLine("{");

			AppendBytes(sb, data, indent + "\t");

			sb.Append(indent)
				.AppendLine("};");
		}

		private static void AppendBytes(StringBuilder sb, ReadOnlySpan<byte> data, string indent)
		{
			for (int i = 0; i < data.Length; i += 32)
			{
				sb.Append(indent);
				int max = Math.Min(32, data.Length - i);
				for (int j = 0; j < max; j++)
				{
					sb.Append("0x")
						.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture))
						.Append(j < 31 ? ", " : ",");
				}
				sb.AppendLine();
			}
		}

		[DebuggerDisplay("{Text} ⊂ {ContainingString}")]
		private sealed class StringInfo
		{
			public StringInfo(string text)
			{
				Text = text;
				LengthInUtf8 = Encoding.UTF8.GetByteCount(text);
			}

			public string Text { get; }
			public int? Index { get; set; }
			public string? ContainingString { get; set; }
			public int LengthInUtf8 { get; }
		}

		// Index principle: The lookup is algorithm is binary search on ID, and data is contiguous between vendors.
		// We can know where the data starts and end by only knowing the total number of encoded devices before and after an entry.
		// As long as we don't need to encode more than 2^16 - 1 devices total, This information can be encoded in 16 bits.
		// Data associated with a vendor consists of a string reference (32 bits) for the name and a list of DeviceInfo (48 bits) structs.
		// As such, the offset to the data of an entry with Index > 0 is Index * SizeOf(OffsetAndShortLength) + Vendor[Index - 1].TotalDeviceCount * SizeOf(DeviceInfo)
		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct VendorIndexEntry
		{
			// Public ID of this vendor.
			public ushort Id;

			// Number of devices encoded including this vendor.
			// Number of devices for this vendor can be computed by looking at previous entry,
			// except for first entry where this is already the value.
			public ushort TotalDeviceCount;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct DeviceIndexEntry
		{
			// Public ID of this device.
			public ushort Id;

			// Reference to the name of the device in the string table.
			public OffsetAndShortLength NameReference;
		}

		// Encodes a reference in the string table using only 32 bits.
		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct OffsetAndShortLength
		{
			private ushort _l;
			private byte _h;
			public int DataIndex
			{
				get => _l | _h << 16;
				set
				{
					_l = (ushort)value;
					_h = checked((byte)unchecked(value >> 16));
				}
			}
			public byte DataLength;
		}
	}
}
