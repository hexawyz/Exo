using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace AnyLayout.SourceGenerators
{
	class Program
	{
		//[DebuggerDisplay("{Text} ⊂ {ContainingString}")]
		//private sealed class StringInfo
		//{
		//	public StringInfo(string text)
		//	{
		//		Text = text;
		//		LengthInUtf8 = Encoding.UTF8.GetByteCount(text);
		//	}

		//	public string Text { get; }
		//	public int? Index { get; set; }
		//	public string? ContainingString { get; set; }
		//	public int LengthInUtf8 { get; }
		//}

		//// Index principle: The lookup is algorithm is binary search on ID, and data is contiguous between vendors.
		//// We can know where the data starts and end by only knowing the total number of encoded devices before and after an entry.
		//// As long as we don't need to encode more than 2^16 - 1 devices total, This information can be encoded in 16 bits.
		//// Data associated with a manufacturer consists of a string reference (32 bits) for the name and a list of DeviceInfo (48 bits) structs.
		//// As such, the offset to the data of an entry with Index > 0 is Index * SizeOf(OffsetAndShortLength) + Manufacturer[Index - 1].TotalDeviceCount * SizeOf(DeviceInfo)
		//[StructLayout(LayoutKind.Sequential, Pack = 2)]
		//private struct ManufacturerIndexEntry
		//{
		//	// Public ID of this vendor.
		//	public ushort Id;

		//	// Number of devices encoded including this vendor.
		//	// Number of devices for this vendor can be computed by looking at previous entry,
		//	// except for first entry where this is already the value.
		//	public ushort TotalDeviceCount;
		//}

		//[StructLayout(LayoutKind.Sequential, Pack = 2)]
		//private struct DeviceIndexEntry
		//{
		//	// Public ID of this device.
		//	public ushort Id;

		//	// Reference to the name of the device in the string table.
		//	public OffsetAndShortLength NameReference;
		//}

		//// Encodes a reference in the string table using only 32 bits.
		//[StructLayout(LayoutKind.Sequential, Pack = 2)]
		//private struct OffsetAndShortLength
		//{
		//	private ushort _l;
		//	private byte _h;
		//	public int DataIndex
		//	{
		//		get => _l | _h << 16;
		//		set
		//		{
		//			_l = (ushort)value;
		//			_h = checked((byte)unchecked(value >> 16));
		//		}
		//	}
		//	public byte DataLength;
		//}

		//private static void Main(string[] args)
		//{
		//	// Very quick and dirty parsing of USB IDs into a data structure readable in C#.
		//	var httpClient = new HttpClient();
		//	//var streamReader = new StreamReader(httpClient.GetStreamAsync("http://www.linux-usb.org/usb.ids").Result);
		//	var streamReader = File.OpenText("usb.ids");
		//	var vendors = new List<(ushort VendorId, string VendorName, List<(ushort ProductId, string ProductName)> Products)>();
		//	var currentProducts = null as List<(ushort ProductId, string ProductName)>;
		//	while (streamReader.ReadLine() is string line && line != "# List of known device classes, subclasses and protocols")
		//	{
		//		if (line is { Length: 0 } || line.StartsWith('#')) continue;

		//		if (Regex.Match(line, @"(?<Id>[0-9a-fA-F]{4})  (?<Name>.*)$", RegexOptions.ExplicitCapture) is Match match && match.Success)
		//		{
		//			var id = ushort.Parse(match.Groups["Id"].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
		//			var name = match.Groups["Name"].Value;
		//			if (line.StartsWith('\t'))
		//			{
		//				if (currentProducts is null) throw new Exception();
		//				currentProducts.Add((id, name));
		//			}
		//			else
		//			{
		//				currentProducts = new List<(ushort ProductId, string Name)>();
		//				vendors.Add((id, name, currentProducts));
		//			}
		//		}
		//		else
		//		{
		//			throw new Exception();
		//		}
		//	}

		//	var stringMap =
		//	(
		//		from v in vendors
		//		from name in
		//		(
		//			from p in v.Products
		//			select p.ProductName
		//		).Prepend(v.VendorName)
		//		select name
		//	)
		//		.Distinct()
		//		.ToDictionary(s => s, s => new StringInfo(s));

		//	var strings = stringMap.Values.ToArray();

		//	// Identify strings contained in other strings
		//	// This is brute force and could very certainly be improved.
		//	for (int i = 0; i < strings.Length; i++)
		//	{
		//		var info1 = strings[i];
		//		for (int j = i + 1; j < strings.Length; j++)
		//		{
		//			var info2 = strings[j];

		//			if (info1.Text.Contains(info2.Text, StringComparison.Ordinal))
		//			{
		//				info2.ContainingString ??= info1.Text;
		//			}
		//			else if (info2.Text.Contains(info1.Text, StringComparison.Ordinal))
		//			{
		//				info1.ContainingString ??= info2.Text;
		//			}
		//		}
		//	}

		//	// Flatten the hierarchy of strings
		//	int changeCount;
		//	do
		//	{
		//		changeCount = 0;
		//		foreach (var info in strings)
		//		{
		//			string? finalContainer = null;
		//			var item = info;
		//			while (item.ContainingString is string container)
		//			{
		//				finalContainer = container;
		//				item = stringMap[container];
		//			}

		//			if (info.ContainingString != finalContainer)
		//			{
		//				info.ContainingString = finalContainer;
		//				changeCount++;
		//			}
		//		}
		//	} while (changeCount > 0);

		//	int currentIndex = 0;
		//	foreach (var info in strings)
		//	{
		//		if (info.ContainingString is not null) continue;
		//		info.Index = currentIndex;
		//		currentIndex += info.LengthInUtf8;
		//	}

		//	int totalStringLength = 0;
		//	foreach (var info in strings)
		//	{
		//		if (info.ContainingString is null)
		//		{
		//			totalStringLength += info.LengthInUtf8;
		//		}
		//		else
		//		{
		//			info.Index = stringMap[info.ContainingString].Index + info.ContainingString.IndexOf(info.Text, StringComparison.Ordinal);
		//		}
		//	}

		//	// Fill string data
		//	var stringData = new byte[totalStringLength];
		//	foreach (var info in strings)
		//	{
		//		if (info.ContainingString is not null) continue;

		//		Encoding.UTF8.GetBytes(info.Text, 0, info.Text.Length, stringData, info.Index.GetValueOrDefault());
		//	}

		//	var deviceCount = vendors.Sum(v => v.Products.Count);

		//	var deviceIndexData = new byte[vendors.Count * Unsafe.SizeOf<OffsetAndShortLength>() + deviceCount * Unsafe.SizeOf<DeviceIndexEntry>()];
		//	var manufacturerIndexData = new ManufacturerIndexEntry[vendors.Count];

		//	// Fill index data.
		//	{
		//		int currentDeviceCount = 0;
		//		var unfilledData = deviceIndexData.AsSpan();

		//		for (int i = 0; i < vendors.Count; i++)
		//		{
		//			var (vendorId, vendorName, products) = vendors[i];

		//			MemoryMarshal.Cast<byte, OffsetAndShortLength>(unfilledData.Slice(0, Unsafe.SizeOf<OffsetAndShortLength>()))[0] = GetStringReference(stringMap, vendorName);
		//			unfilledData = unfilledData.Slice(Unsafe.SizeOf<OffsetAndShortLength>());
		//			int deviceByteCount = products.Count * Unsafe.SizeOf<DeviceIndexEntry>();
		//			var devices = MemoryMarshal.Cast<byte, DeviceIndexEntry>(unfilledData.Slice(0, deviceByteCount));
		//			for (int j = 0; j < products.Count; j++)
		//			{
		//				var (productId, productName) = products[j];
		//				devices[j] = new DeviceIndexEntry
		//				{
		//					Id = productId,
		//					NameReference = GetStringReference(stringMap, productName)
		//				};
		//			}
		//			unfilledData = unfilledData.Slice(deviceByteCount);

		//			manufacturerIndexData[i] = new ManufacturerIndexEntry
		//			{
		//				Id = vendorId,
		//				TotalDeviceCount = checked((ushort)unchecked(currentDeviceCount += products.Count))
		//			};
		//		}
		//	}

		//	OutputSpan("VendorIndexData", MemoryMarshal.Cast<ManufacturerIndexEntry, byte>(manufacturerIndexData));
		//	OutputSpan("DeviceIndexData", deviceIndexData);
		//	OutputSpan("StringData", stringData);
		//}

		//static OffsetAndShortLength GetStringReference(Dictionary<string, StringInfo> map, string text)
		//{
		//	var stringInfo = map[text];
		//	return new OffsetAndShortLength
		//	{
		//		DataIndex = stringInfo.Index.GetValueOrDefault(),
		//		DataLength = checked((byte)stringInfo.LengthInUtf8),
		//	};
		//}

		//static void OutputSpan(string name, ReadOnlySpan<byte> data)
		//{
		//	Console.WriteLine("public static ReadOnlySpan<byte> {0} =>", name);
		//	Console.WriteLine("{");
		//	Console.Write(FormatBytes(data, "\t"));
		//	Console.WriteLine("}");
		//}

		//static string FormatBytes(ReadOnlySpan<byte> data, string linePrefix)
		//{
		//	var sb = new StringBuilder();
		//	for (int i = 0; i < data.Length; i += 16)
		//	{
		//		sb.Append(linePrefix);
		//		int max = Math.Min(16, data.Length - i);
		//		for (int j = 0; j < max; j++)
		//		{
		//			sb.Append("0x")
		//				.Append(data[i + j].ToString("X2", CultureInfo.InvariantCulture))
		//				.Append(j < 15 ? ", " : ",");
		//		}
		//		sb.AppendLine();
		//	}

		//	return sb.ToString();
		//}
	}
}
