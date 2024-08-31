using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools
{
	public static partial class UsbProductNameDatabase
	{
#if false
		private static ReadOnlySpan<byte> VendorIndexData => new byte[] { 0 };
		private static ReadOnlySpan<byte> ProductIndexData => new byte[] { 0 };
		internal static ReadOnlySpan<byte> StringData => new byte[] { 0 };
#endif

		private interface IIndexEntry
		{
			ushort Id { get; }
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private readonly struct VendorIndexEntry : IIndexEntry
		{
			// Public ID of this vendor.
			public ushort Id { get; }

			// Number of devices encoded including this vendor.
			// Number of devices for this vendor can be computed by looking at previous entry,
			// except for first entry where this is already the value.
			public ushort TotalProductCount { get; }
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private readonly struct ProductIndexEntry : IIndexEntry
		{
			// Public ID of this device.
			public ushort Id { get; }

			// Reference to the name of the device in the string table.
			public ProductDatabaseName NameReference { get; }
		}

		private static int IndexOfId<T>(ReadOnlySpan<T> items, ushort id)
			where T : IIndexEntry
		{
			int min = 0;
			int max = id < items.Length ? id : items.Length - 1;

			do
			{
				int med = (min + max) >> 1;

				int currentId = items[med].Id;

				if (currentId == id) return med;
				else if (currentId < id) min = med + 1;
				else max = med - 1;
			}
			while (min <= max);

			return -1;
		}

		public static ProductDatabaseName LookupVendorName(ushort vendorId)
			=> MemoryMarshal.Cast<byte, VendorIndexEntry>(VendorIndexData) is var vendorIndexData &&
				IndexOfId(vendorIndexData, vendorId) is int vendorIndex &&
				vendorIndex >= 0 ?
					MemoryMarshal.Read<ProductDatabaseName>(ProductIndexData.Slice(vendorIndex * Unsafe.SizeOf<ProductDatabaseName>() +
						(vendorIndex > 0 ? vendorIndexData[vendorIndex - 1].TotalProductCount * Unsafe.SizeOf<ProductIndexEntry>() : 0))) :
					default;

		public static (ProductDatabaseName VendorName, ProductDatabaseName ProductName) LookupVendorAndProductName(ushort vendorId, ushort productId)
		{
			if (MemoryMarshal.Cast<byte, VendorIndexEntry>(VendorIndexData) is var vendorIndexData && IndexOfId(vendorIndexData, vendorId) is int vendorIndex && vendorIndex >= 0)
			{
				int numberOfProductsBefore = vendorIndex > 0 ? vendorIndexData[vendorIndex - 1].TotalProductCount : 0;
				int productCount = vendorIndexData[vendorIndex].TotalProductCount - numberOfProductsBefore;

				int vendorDataOffset = vendorIndex * Unsafe.SizeOf<ProductDatabaseName>() + numberOfProductsBefore * Unsafe.SizeOf<ProductIndexEntry>();

				var vendorName = MemoryMarshal.Read<ProductDatabaseName>(ProductIndexData.Slice(vendorDataOffset));
				var productIndexData = MemoryMarshal.Cast<byte, ProductIndexEntry>(ProductIndexData.Slice(vendorDataOffset + Unsafe.SizeOf<ProductDatabaseName>(), productCount * Unsafe.SizeOf<ProductIndexEntry>()));

				var productName = IndexOfId(productIndexData, productId) is int productIndex && productIndex >= 0 ?
					productIndexData[productIndex].NameReference :
					default;

				return (vendorName, productName);
			}
			return default;
		}
	}

	// Encodes a reference in the string table using only 32 bits.
	[StructLayout(LayoutKind.Sequential, Pack = 2)]
	public readonly struct ProductDatabaseName
	{
		private readonly ushort _l;
		private readonly byte _h;
		private readonly byte _dataLength;

		public int Length => _dataLength;

		public ReadOnlySpan<byte> GetUtf8String() => UsbProductNameDatabase.StringData.Slice(_l | _h << 16, _dataLength);

#if NETSTANDARD2_0
		public unsafe override string ToString()
			// Provided the memory is at a fixed place in memory (CIL bytearray literal), this is safe. Otherwise, we're doomed and I'm sorry.
			=> Encoding.UTF8.GetString((byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(UsbProductNameDatabase.StringData.Slice(_l | _h << 16))), _dataLength);
#else
		public override string ToString() => Encoding.UTF8.GetString(GetUtf8String());
#endif
	}
}
