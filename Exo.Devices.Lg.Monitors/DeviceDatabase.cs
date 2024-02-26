using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DeviceTools;

namespace Exo.Devices.Lg.Monitors;

internal readonly struct MonitorDeviceInformation
{
	public required string ModelName { get; init; }
	public required ImmutableArray<DeviceId> DeviceIds { get; init; }
}

internal static partial class DeviceDatabase
{
	internal const ushort LgPnpVendorId = 0x1e6d;
	internal const ushort LgUsbVendorId = 0x043E;

	private readonly struct IndexEntry
	{
		private readonly ushort _productId;
		private readonly ushort _index;

		public ushort ProductId => LittleEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _productId)));
		public ushort Index => LittleEndian.ReadUInt16(in Unsafe.As<ushort, byte>(ref Unsafe.AsRef(in _index)));
	}

	private static MonitorDeviceInformation GetMonitorInformationFromIndex(ushort index)
	{
		bool isUsb = index < UsbMonitorCount;
		var details = Details.Slice(index * 6, 6);
		var modelName = Strings[LittleEndian.ReadUInt16(in details[0])];
		var deviceIds = GetDeviceIds(LittleEndian.ReadUInt16(in details[2]), details[4], isUsb);
		return new()
		{
			ModelName = modelName,
			DeviceIds = deviceIds,
		};
	}

	public static MonitorDeviceInformation GetMonitorInformationFromMonitorProductId(ushort productId)
	{
		var entries = MemoryMarshal.Cast<byte, IndexEntry>(Index);

		int min = 0;
		int max = entries.Length - 1;

		while (min <= max)
		{
			int med = (min + max) >>> 1;
			ref readonly var entry = ref entries[med];

			int delta = productId - entry.ProductId;
			if (delta == 0)
			{
				return GetMonitorInformationFromIndex(entry.Index);
			}
			else if (delta > 0)
			{
				min = med + 1;
			}
			else
			{
				max = med - 1;
			}
		}
		throw new KeyNotFoundException();
	}

	public static MonitorDeviceInformation GetMonitorInformationFromModelName(string modelName)
		=> GetMonitorInformationFromIndex(MonitorIndicesByName[modelName]);

	private static ImmutableArray<DeviceId> GetDeviceIds(ushort offset, byte length, bool isUsbMonitor)
	{
		if (length == 0) return [];

		var productIds = MemoryMarshal.Cast<byte, ushort>(ProductIds.Slice(offset, length * 2));
		var deviceIds = new DeviceId[productIds.Length];

		int i = 0;
		if (isUsbMonitor)
		{
			deviceIds[i] = DeviceId.ForUsb(LgUsbVendorId, productIds[i], 0xFFFF);
			i++;
		}
		for (; i < productIds.Length; i++)
		{
			deviceIds[i] = DeviceId.ForDisplay(PnpVendorId.FromRaw(LgPnpVendorId), productIds[i]);
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(deviceIds);
	}
}
