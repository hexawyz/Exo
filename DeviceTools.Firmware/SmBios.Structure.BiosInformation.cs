using System.Globalization;

namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class BiosInformation : Structure
		{
			public override byte Type => 0;

			public string? Vendor { get; }
			public string? BiosVersion { get; }
			public DateTime? BiosReleaseDate { get; }

			public ushort BiosStartingAddressSegment { get; }
			public ulong BiosRomSize { get; }
			public BiosCharacteristics BiosCharacteristics { get; }
			public uint VendorBiosCharacteristics { get; }
			public ExtendedBiosCharacteristics ExtendedBiosCharacteristics { get; }

			public byte SystemBiosMajorRelease { get; }
			public byte SystemBiosMinorRelease { get; }

			public byte EmbeddedFirmwareControllerMajorRelease { get; }
			public byte EmbeddedFirmwareControllerMinorRelease { get; }

			internal BiosInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.0+
				if (data.Length < 14) throw new InvalidDataException("The data structure for BIOS Information is not long enough.");

				Vendor = GetString(strings, data[0]);
				BiosVersion = GetString(strings, data[1]);
				BiosStartingAddressSegment = LittleEndian.Read<ushort>(data[2..]);
				var biosReleaseDate = GetString(strings, data[4]);
				if (biosReleaseDate is not null)
				{
					if (biosReleaseDate.Length < 8 || biosReleaseDate.Length == 9 || biosReleaseDate[2] != '/' || biosReleaseDate[5] != '/')
					{
						throw new InvalidDataException("Invalid BIOS release date.");
					}

					byte month = byte.Parse(biosReleaseDate.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture);
					byte day = byte.Parse(biosReleaseDate.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture);
					ushort year;
					if (biosReleaseDate.Length == 8)
					{
						year = (ushort)(1900 + byte.Parse(biosReleaseDate.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture));
					}
					else
					{
						year = ushort.Parse(biosReleaseDate.AsSpan(6), NumberStyles.None, CultureInfo.InvariantCulture);
					}
					BiosReleaseDate = new DateTime(year, month, day, 0, 0, 0, 0, DateTimeKind.Utc);
				}
				byte romSize = data[5];
				BiosRomSize = (romSize + 1U) * (64 * 1024);

				// BIOS Characteristics are supposed to be a single 64 bit field, but only the first 32 bits are explicitly defined.
				// Other 32 bits are vendor-defined. As such, splitting the characteristics in two makes sense.
				BiosCharacteristics = (BiosCharacteristics)LittleEndian.Read<uint>(data[6..]);
				VendorBiosCharacteristics = LittleEndian.Read<uint>(data[10..]);

				EmbeddedFirmwareControllerMinorRelease =
					EmbeddedFirmwareControllerMajorRelease =
					SystemBiosMinorRelease =
					SystemBiosMajorRelease = 255;

				// SMBIOS 2.1+
				if (data.Length >= 15)
				{
					// SMBIOS 2.3+
					if (data.Length >= 16)
					{
						ExtendedBiosCharacteristics = (ExtendedBiosCharacteristics)LittleEndian.Read<ushort>(data[14..]);

						// SMBIOS 2.4+
						if (data.Length >= 20)
						{
							SystemBiosMajorRelease = data[16];
							SystemBiosMinorRelease = data[17];
							EmbeddedFirmwareControllerMajorRelease = data[18];
							EmbeddedFirmwareControllerMinorRelease = data[19];

							// SMBIOS 3.1+
							if (data.Length >= 22 && romSize == 0xFF)
							{
								ushort extendedRomSize = LittleEndian.Read<ushort>(data[20..]);
								byte unit = (byte)(extendedRomSize >>> 14);
								extendedRomSize &= 0x3FFF;
								BiosRomSize = unit switch
								{
									0 => extendedRomSize * (1024UL * 1024),
									1 => extendedRomSize * (1024UL * 1024 * 1024),
									_ => throw new InvalidOperationException("Unsupported BIOS ROM size."),
								};
							}
						}
					}
					else
					{
						ExtendedBiosCharacteristics = (ExtendedBiosCharacteristics)data[14];
					}
				}
			}
		}
	}
}
