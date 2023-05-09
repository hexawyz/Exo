namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class SystemInformation : Structure
		{
			public override byte Type => 1;

			public string? Manufacturer { get; }
			public string? ProductName { get; }
			public string? Version { get; }
			public string? SerialNumber { get; }
			public Guid? Uuid { get; }
			public SystemWakeUpType? WakeUpType { get; }
			public string? SkuNumber { get; }
			public string? Family { get; }

			internal SystemInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.0+
				if (data.Length < 4) throw new InvalidDataException("The data structure for Processor Information is not long enough.");

				Manufacturer = GetString(strings, data[0]);
				ProductName = GetString(strings, data[1]);
				Version = GetString(strings, data[2]);
				SerialNumber = GetString(strings, data[3]);

				// SMBIOS 2.1+
				if (data.Length >= 21)
				{
					Uuid = LittleEndian.Read<Guid>(data[4..]);
					WakeUpType = (SystemWakeUpType)data[20];

					// SMBIOS 2.4+
					if (data.Length >= 23)
					{
						SkuNumber = GetString(strings, data[21]);
						Family = GetString(strings, data[22]);
					}
				}
			}
		}
	}
}
