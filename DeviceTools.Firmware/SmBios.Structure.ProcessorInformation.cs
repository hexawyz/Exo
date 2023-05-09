namespace DeviceTools.Firmware;

public sealed partial class SmBios
{
	public abstract partial class Structure
	{
		public sealed class ProcessorInformation : Structure
		{
			public override byte Type => 4;

			public string? SocketDesignation { get; }
			public string? ProcessorManufacturer { get; }
			public string? ProcessorVersion { get; }
			public string? SerialNumber { get; }
			public string? AssetTag { get; }
			public string? PartNumber { get; }

			public ProcessorType ProcessorType { get; }
			public ProcessorFamily ProcessorFamily { get; }
			public ulong ProcessorId { get; }
			public byte Voltage { get; }
			public uint ExternalClock { get; }
			public uint MaximumSpeed { get; }
			public uint CurrentSpeed { get; }

			public ushort? CoreCount { get; }
			public ushort? EnabledCoreCount { get; }
			public ushort? ThreadCount { get; }
			public ushort? EnabledThreadCount { get; }
			public ProcessorCharacteristics ProcessorCharacteristics { get; }

			private readonly byte _status;

			public bool IsSocketPopulated => (_status & 0x40) != 0;
			public CpuStatus CpuStatus => (CpuStatus)(_status & 0x07);
			public ProcessorUpgrade ProcessorUpgrade { get; }

			internal ProcessorInformation(ushort handle, ReadOnlySpan<byte> data, List<string> strings) : base(handle)
			{
				// SMBIOS 2.0+
				if (data.Length < 22) throw new InvalidDataException("The data structure for Processor Information is not long enough.");

				SocketDesignation = GetString(strings, data[0]);
				ProcessorType = (ProcessorType)data[1];
				ProcessorFamily = (ProcessorFamily)data[2];
				ProcessorManufacturer = GetString(strings, data[3]);
				ProcessorId = LittleEndian.Read<ulong>(data[4..]);
				ProcessorVersion = GetString(strings, data[12]);
				Voltage = data[13];
				ExternalClock = LittleEndian.Read<ushort>(data[14..]) * 1000u * 1000;
				MaximumSpeed = LittleEndian.Read<ushort>(data[16..]) * 1000u * 1000;
				CurrentSpeed = LittleEndian.Read<ushort>(data[18..]) * 1000u * 1000;
				_status = data[20];
				ProcessorUpgrade = (ProcessorUpgrade)data[21];

				// SMBIOS 2.1+
				if (data.Length >= 28)
				{
					// SMBIOS 2.3+
					if (data.Length >= 31)
					{
						SerialNumber = GetString(strings, data[28]);
						AssetTag = GetString(strings, data[29]);
						PartNumber = GetString(strings, data[30]);

						// SMBIOS 2.5+
						if (data.Length >= 36)
						{
							CoreCount = data[31];
							EnabledCoreCount = data[32];
							ThreadCount = data[33];
							ProcessorCharacteristics = (ProcessorCharacteristics)LittleEndian.Read<ushort>(data[34..]);

							// SMBIOS 2.6+
							if (data.Length >= 38)
							{
								ProcessorFamily = (ProcessorFamily)LittleEndian.Read<ushort>(data[36..]);

								// SMBIOS 3.0+
								if (data.Length >= 44)
								{
									CoreCount = LittleEndian.Read<ushort>(data[38..]);
									EnabledCoreCount = LittleEndian.Read<ushort>(data[40..]);
									ThreadCount = LittleEndian.Read<ushort>(data[42..]);

									// SMBIOS 3.6+
									if (data.Length >= 46)
									{
										EnabledThreadCount = LittleEndian.Read<ushort>(data[44..]);
									}
								}
							}
						}
					}
				}
			}
		}
	}
}
