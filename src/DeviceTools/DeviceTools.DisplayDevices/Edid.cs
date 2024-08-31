using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace DeviceTools.DisplayDevices;

/// <summary>Provide EDID information.</summary>
/// <remarks>
/// Until further notice, this class does not decode 100% of the EDID data.
/// Future evolutions will improve the completeness of the EDID parsing.
/// </remarks>
// TODO: Parse more stuff. (Initial version only parses the most useful and strictly required stuff for proper function of the services)
public class Edid
{
	private static ReadOnlySpan<byte> EdidHeader => [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

	public static Edid Parse(ReadOnlySpan<byte> data)
	{
		if (data.Length == 0 || (data.Length & 0x7F) != 0)
		{
			throw new ArgumentException("Data must be composed exclusively of 128 byte blocks.");
		}

		if (!data[..8].SequenceEqual(EdidHeader))
		{
			throw new InvalidDataException("Invalid EDID header.");
		}

		ValidateChecksum(data[..128]);

		var vendorId = PnpVendorId.FromRaw(BigEndian.ReadUInt16(in data[8]));
		ushort productId = LittleEndian.ReadUInt16(in data[10]);
		uint idSerialNumber = LittleEndian.ReadUInt32(in data[12]);
		string? productName = null;
		string? serialNumber = null;

		int timingDescriptorCount = 0;

		for (int i = 0; i < 4; i++)
		{
			var block = data.Slice(54 + 18 * i, 18);

			ushort pixelClock = LittleEndian.ReadUInt16(in block[0]);

			if (pixelClock != 0)
			{
				if (timingDescriptorCount != i) throw new InvalidDataException("The 18 byte descriptors are not correctly ordered.");
				timingDescriptorCount++;
				continue;
			}
			else if (timingDescriptorCount == 0)
			{
				throw new InvalidDataException("Missing a timing descriptor as the first 18 byte descriptor.");
			}

			switch (block[3])
			{
			case 0xFC:
				productName = ParseDescriptorString(block[5..]);
				break;
			case 0xFF:
				serialNumber = ParseDescriptorString(block[5..]);
				break;
			}
		}

		return new Edid
		(
			vendorId,
			productId,
			idSerialNumber,
			productName,
			serialNumber
		);
	}

	private static void ValidateChecksum(ReadOnlySpan<byte> data)
	{
		if (ComputeChecksum(data) != 0) throw new InvalidDataException("Checksum of the block was invalid.");
	}

	private static byte ComputeChecksum(ReadOnlySpan<byte> data)
		=> Avx2.IsSupported ? ComputeChecksumAvx2(data) : ComputeChecksumSlow(data);

	private static byte ComputeChecksumAvx2(ReadOnlySpan<byte> data)
	{
		if (data.Length != 128) throw new ArgumentException();

		var vectors = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(data)), 4);

		// Quickly sum groups of 32 bytes, then try to reduce the sum in as few operations as possible.
		var sum32 = Avx2.Add(Avx2.Add(vectors[0], vectors[1]), Avx2.Add(vectors[2], vectors[3]));
		// This will give us the equivalent of four 64 bit integers containing the sums of 8 bytes each.
		var sum4 = Avx2.SumAbsoluteDifferences(sum32, default).AsUInt32();
		// Sums the two halves of the previous vector in 32 bits mode (it might be faster than 64 bit additions)
		var sum2 = Sse2.Add(sum4.GetLower(), sum4.GetUpper());

		// NB: For performance, it is better to get the whole 4 bytes of element zero, as it will avoid a zero-extend operation, but it won't change anything for the second one.
		return (byte)(sum2.GetElement(0) + sum2.AsByte().GetElement(8));
	}

	private static byte ComputeChecksumSlow(ReadOnlySpan<byte> data)
	{
		int sum = data[0];

		for (int i = 1; i < 128; i++)
		{
			sum += data[i];
		}

		return (byte)sum;
	}

	private static string? ParseDescriptorString(ReadOnlySpan<byte> data)
	{
		int endIndex = data.IndexOf((byte)0x0A);

		if (endIndex < 0) endIndex = data.Length;

		return endIndex != 0 ? Encoding.UTF8.GetString(data[..endIndex]) : null;
	}

	public PnpVendorId VendorId { get; }
	public ushort ProductId { get; }
	public uint IdSerialNumber { get; }
	public string? ProductName;
	public string? SerialNumber;

	private Edid(PnpVendorId vendorId, ushort productId, uint idSerialNumber, string? productName, string? serialNumber)
	{
		VendorId = vendorId;
		ProductId = productId;
		IdSerialNumber = idSerialNumber;
		ProductName = productName;
		SerialNumber = serialNumber;
	}
}
