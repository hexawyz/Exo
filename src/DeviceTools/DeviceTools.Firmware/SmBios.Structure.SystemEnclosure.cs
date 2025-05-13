namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class SystemEnclosure : Structure
		{
			public override byte Type => 3;

			private readonly string? _manufacturer;
			private readonly string? _version;
			private readonly string? _serialNumber;
			private readonly string? _assetTagNumber;
			private readonly string? _skuNumber;
			private readonly uint _oemDefined;
			private readonly byte _type;
			private readonly SystemEnclosureState _bootUpState;
			private readonly SystemEnclosureState _powerSupplyState;
			private readonly SystemEnclosureState _thermalState;
			private readonly SystemEnclosureSecurityStatus _securityStatus;
			private readonly byte _height;
			private readonly byte _numberOfPowerCords;
			private readonly byte _containedElementCount;
			private readonly byte _containedElementRecordLength;

			public string? Manufacturer => _manufacturer;
			public bool HasChassisLock => (sbyte)_type < 0;
			public SystemEnclosureType EnclosureType => (SystemEnclosureType)(_type & 0x7F);
			public string? Version => _version;
			public string? SerialNumber => _serialNumber;
			public string? AssetTagNumber => _assetTagNumber;
			public SystemEnclosureState BootUpState => _bootUpState;
			public SystemEnclosureState PowerSupplyState => _powerSupplyState;
			public SystemEnclosureState ThermalState => _thermalState;
			public SystemEnclosureSecurityStatus SecurityStatus => _securityStatus;
			public uint OemDefined => _oemDefined;
			public byte Height => _height;
			public byte PowerCordCount => _numberOfPowerCords;
			public string? SkuNumber => _skuNumber;

			internal SystemEnclosure(ushort handle, ReadOnlySpan<byte> data, List<string> strings, byte majorVersion, byte minorVersion) : base(handle)
			{
				byte structVersion = 0;

				if (majorVersion == 2)
				{
					// SMBIOS 2.0
					if (minorVersion == 0) structVersion = 1;
					// SMBIOS 2.1+
					else if (minorVersion < 3) structVersion = 2;
					// SMBIOS 2.3+
					else if (minorVersion < 7) structVersion = 3;
					// SMBIOS 2.7+
					else structVersion = 4;
				}
				else if (majorVersion > 2)
				{
					// SMBIOS 2.7+
					structVersion = 4;
				}

				byte requiredLength = structVersion switch
				{
					1 => 5,
					2 => 9,
					3 => 17,
					4 => 18,
					_ => throw new InvalidDataException("The data structure for System Enclosure is not defined for this SMBIOS version."),
				};

				if (data.Length < requiredLength) throw new InvalidDataException("The data structure for System Enclosure has an invalid length.");

				_manufacturer = GetString(strings, data[0]);
				_type = data[1];
				_version = GetString(strings, data[2]);
				_serialNumber = GetString(strings, data[3]);
				_assetTagNumber = GetString(strings, data[4]);
				if (structVersion < 2) return;
				_bootUpState = (SystemEnclosureState)data[5];
				_powerSupplyState = (SystemEnclosureState)data[6];
				_thermalState = (SystemEnclosureState)data[7];
				_securityStatus = (SystemEnclosureSecurityStatus)data[8];
				if (structVersion < 3) return;
				_oemDefined = LittleEndian.Read<uint>(data[9..]);
				_height = data[13];
				_numberOfPowerCords = data[14];
				byte containedElementCount = data[15];
				byte containedElementRecordLength = data[16];
				uint variableLength = (uint)containedElementCount * containedElementRecordLength;
				if ((uint)data.Length < variableLength + (structVersion < 4 ? 17U : 18)) throw new InvalidDataException("The data structure for System Enclosure has an invalid length.");
				// TODO: Read variable stuff
				if (structVersion < 4) return;
				_skuNumber = GetString(strings, data[(int)(17 + variableLength)]);
			}
		}
	}
}
