using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class BaseboardInformation : Structure
		{
			public override byte Type => 2;

			private readonly string? _manufacturer;
			private readonly string? _product;
			private readonly string? _version;
			private readonly string? _serialNumber;
			private readonly string? _assetTag;
			private readonly string? _locationInChassis;
			private readonly ImmutableArray<ushort> _containedObjectHandles;
			private readonly BaseboardFeatureFlags _featureFlags;
			private readonly byte _boardType;
			private readonly ushort _chassisHandle;

			public string? Manufacturer => _manufacturer;
			public string? Product => _product;
			public string? Version => _version;
			public string? SerialNumber => _serialNumber;
			public string? AssetTag => _assetTag;
			public BaseboardFeatureFlags FeatureFlags => _featureFlags;
			public string? LocationInChassis => _locationInChassis;
			public byte BoardType => _boardType;

			internal BaseboardInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.0+
				if (data.Length < 4) throw new InvalidDataException("The data structure for Baseboard Information is not long enough.");

				_manufacturer = GetString(strings, data[0]);
				_product = GetString(strings, data[1]);
				_version = GetString(strings, data[2]);
				_serialNumber = GetString(strings, data[3]);
				if (data.Length <= 4) return;
				_assetTag = GetString(strings, data[4]);
				if (data.Length <= 5) return;
				_featureFlags = (BaseboardFeatureFlags)data[5];
				if (data.Length <= 6) return;
				_locationInChassis = GetString(strings, data[6]);
				if (data.Length <= 7) return;
				_chassisHandle = LittleEndian.Read<ushort>(data[7..]);
				if (data.Length <= 9) return;
				_boardType = data[9];
				if (data.Length <= 10) return;
				uint containedObjectCount = data[10];
				if (data.Length <= 10 + 2 * containedObjectCount) throw new InvalidDataException("The data structure for Baseboard Information contains invalid data.");
				if (containedObjectCount == 0)
				{
					_containedObjectHandles = [];
				}
				else
				{
					data = data[11..];
					var containedObjectHandles = new ushort[containedObjectCount];
					for (int i = 0; i < containedObjectHandles.Length; i++)
					{
						containedObjectHandles[i] = LittleEndian.Read<ushort>(data);
						data = data[2..];
					}
					_containedObjectHandles = ImmutableCollectionsMarshal.AsImmutableArray(containedObjectHandles);
				}
			}
		}
	}
}
