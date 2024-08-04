namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class MemoryDevice : Structure
		{
			public override byte Type => 17;

			public string? DeviceLocator { get; }
			public string? BankLocator { get; }
			public string? Manufacturer { get; }
			public string? SerialNumber { get; }
			public string? AssetTag { get; }
			public string? PartNumber { get; }
			public string? FirmwareVersion { get; }

			private readonly ulong _size;
			private readonly ulong _speed;
			private readonly ulong _configuredMemorySpeed;
			private readonly ulong _nonVolatileSize;
			private readonly ulong _volatileSize;
			private readonly ulong _cacheSize;
			private readonly ulong _logicalSize;

			private readonly ushort _physicalMemoryArrayHandle;
			private readonly ushort _memoryErrorInformationHandle;

			private readonly ushort _totalWidth;
			private readonly ushort _dataWidth;

			private readonly ushort _minimumVoltage;
			private readonly ushort _maximumVoltage;
			private readonly ushort _configuredVoltage;

			private readonly byte _formFactor;
			private readonly byte _memoryType;

			private readonly byte _deviceSet;
			private readonly byte _attributes;

			private readonly ushort _typeDetail;

			private readonly byte _nonNullIds;
			private readonly byte _memoryTechnology;
			private readonly ushort _memoryOperatingModeCapability;
			private readonly byte _moduleManufacturerIdContinuationCodeCount;
			private readonly byte _moduleManufacturerIdCode;
			private readonly ushort _moduleProductId;
			private readonly ushort _memorySubsystemControllerManufacturerId;
			private readonly ushort _memorySubsystemControllerProductId;

			public ushort? TotalWidth => _totalWidth != 0xFFFF ? _totalWidth : null;
			public ushort? DataWidth => _dataWidth != 0xFFFF ? _dataWidth : null;
			public ulong? Size => _size != ulong.MaxValue ? _size : null;
			public ulong? Speed => _speed != 0 ? _speed : null;
			public ulong? ConfiguredMemorySpeed => _configuredMemorySpeed != 0 ? _configuredMemorySpeed : null;

			public ushort? MinimumVoltage => _minimumVoltage != 0 ? _minimumVoltage : null;
			public ushort? MaximumVoltage => _maximumVoltage != 0 ? _maximumVoltage : null;
			public ushort? ConfiguredVoltage => _configuredVoltage != 0 ? _configuredVoltage : null;

			public MemoryDeviceFormFactor FormFactor => (MemoryDeviceFormFactor)_formFactor;
			public MemoryDeviceMemoryType MemoryType => (MemoryDeviceMemoryType)_memoryType;

			public bool BelongsToDeviceSet => _deviceSet is not 0 and not 255;
			public byte? DeviceSetNumber => _deviceSet != 0xFF ? _deviceSet : null;

			public MemoryDeviceTypeDetail TypeDetail => (MemoryDeviceTypeDetail)_typeDetail;

			public byte? Rank => (byte)(_attributes & 0xF) is byte rank and not 0 ? rank : null;

			public MemoryDeviceMemoryTechnology MemoryTechnology => (MemoryDeviceMemoryTechnology)_memoryTechnology;
			public MemoryDeviceMemoryOperatingModeCapability OperatingModeCapability => (MemoryDeviceMemoryOperatingModeCapability)_memoryOperatingModeCapability;

			public JedecManufacturerId? ModuleManufacturerId => (_nonNullIds & 0x1) != 0 ? new(_moduleManufacturerIdContinuationCodeCount, _moduleManufacturerIdCode) : null;
			public ushort? ModuleProductId => (_nonNullIds & 0x2) != 0 ? _moduleProductId : null;
			public ushort? MemorySubsystemControllerManufacturerId => (_nonNullIds & 0x4) != 0 ? _memorySubsystemControllerManufacturerId : null;
			public ushort? MemorySubsystemControllerProductId => (_nonNullIds & 0x8) != 0 ? _memorySubsystemControllerProductId : null;

			public ulong? NonVolatileSize => _nonVolatileSize != ulong.MaxValue ? _nonVolatileSize : null;
			public ulong? VolatileSize => _volatileSize != ulong.MaxValue ? _volatileSize : null;
			public ulong? CacheSize => _cacheSize != ulong.MaxValue ? _cacheSize : null;
			public ulong? LogicalSize => _logicalSize != ulong.MaxValue ? _logicalSize : null;

			internal MemoryDevice(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.1+
				if (data.Length < 16) throw new InvalidDataException("The data structure for Memory Device is not long enough.");

				_physicalMemoryArrayHandle = LittleEndian.Read<ushort>(data);
				_memoryErrorInformationHandle = LittleEndian.Read<ushort>(data[2..]);

				_totalWidth = LittleEndian.Read<ushort>(data[4..]);
				_dataWidth = LittleEndian.Read<ushort>(data[6..]);
				ushort size = LittleEndian.Read<ushort>(data[8..]);
				ushort sizeBase = (ushort)(size & 0x7FFF);
				if (sizeBase == 0x7FFF)
				{
					_size = ulong.MaxValue;
				}
				else
				{
					_size = (size & 0x8000) != 0 ? (ulong)sizeBase * 1024 : (ulong)sizeBase * (1024 * 1024);
				}
				_formFactor = data[10];
				_deviceSet = data[11];

				DeviceLocator = GetString(strings, data[12]);
				BankLocator = GetString(strings, data[13]);

				_memoryType = data[14];
				_typeDetail = LittleEndian.Read<ushort>(data[15..]);

				// SMBIOS 2.3+
				if (data.Length >= 23)
				{
					_speed = LittleEndian.Read<ushort>(data[17..]) * (1024UL * 1024);

					Manufacturer = GetString(strings, data[19]);
					SerialNumber = GetString(strings, data[20]);
					AssetTag = GetString(strings, data[21]);
					PartNumber = GetString(strings, data[22]);

					// SMBIOS 2.6+
					if (data.Length >= 24)
					{
						_attributes = data[23];
						// SMBIOS 2.7+
						if (data.Length >= 30)
						{
							if (size == 0x7FFF)
							{
								uint extendedSize = LittleEndian.Read<uint>(data[24..]);
								uint extendedBaseSize = extendedSize & 0x7FFFFFFF;

								_size = extendedBaseSize * (1024UL * 1024);
							}

							_configuredMemorySpeed = LittleEndian.Read<ushort>(data[28..]) * (1024UL * 1024);

							// SMBIOS 2.8+
							if (data.Length >= 36)
							{
								_minimumVoltage = LittleEndian.Read<ushort>(data[30..]);
								_maximumVoltage = LittleEndian.Read<ushort>(data[32..]);
								_configuredVoltage = LittleEndian.Read<ushort>(data[34..]);

								// SMBIOS 3.2+
								if (data.Length >= 80)
								{
									_memoryTechnology = data[36];
									_memoryOperatingModeCapability = LittleEndian.Read<ushort>(data[37..]);

									FirmwareVersion = GetString(strings, data[39]);

									// Optimize the 4 nullable values in a single field.
									byte nonNullIds = 0;
									_moduleManufacturerIdContinuationCodeCount = data[40];
									_moduleManufacturerIdCode = data[41];
									if (_moduleManufacturerIdContinuationCodeCount is not 0 || _moduleManufacturerIdCode is not 0) nonNullIds |= 1;
									if ((_moduleProductId = LittleEndian.Read<ushort>(data[42..])) is not 0) nonNullIds |= 2;
									if ((_memorySubsystemControllerManufacturerId = LittleEndian.Read<ushort>(data[44..])) is not 0) nonNullIds |= 4;
									if ((_memorySubsystemControllerProductId = LittleEndian.Read<ushort>(data[46..])) is not 0) nonNullIds |= 8;
									_nonNullIds = nonNullIds;

									_nonVolatileSize = LittleEndian.Read<ulong>(data[48..]);
									_volatileSize = LittleEndian.Read<ulong>(data[56..]);
									_cacheSize = LittleEndian.Read<ulong>(data[64..]);
									_logicalSize = LittleEndian.Read<ulong>(data[72..]);

									// SMBIOS 3.3+
									if (data.Length >= 88)
									{
										_speed = LittleEndian.Read<uint>(data[80..]) * (1024UL * 1024);
										_configuredMemorySpeed = LittleEndian.Read<uint>(data[84..]) * (1024UL * 1024);
									}
									else
									{
										goto NotVersion3_3;
									}
								}
								else
								{
									goto NotVersion3_2;
								}
							}
							else
							{
								goto NotVersion2_8;
							}
						}
						else
						{
							goto NotVersion2_7;
						}
					}
					else
					{
						goto NotVersion2_6;
					}
				}
				else
				{
					goto NotVersion2_3;
				}
				return;
			NotVersion2_3:;
			NotVersion2_6:;
			NotVersion2_7:;
			NotVersion2_8:;
			NotVersion3_2:;
				_memoryTechnology = (byte)MemoryDeviceMemoryTechnology.Unknown;
				_memoryOperatingModeCapability = (ushort)MemoryDeviceMemoryOperatingModeCapability.Unknown;
			NotVersion3_3:;
				_logicalSize = _cacheSize = _volatileSize = _nonVolatileSize = ulong.MaxValue;
			}
		}
	}
}
