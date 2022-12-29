namespace DeviceTools
{
	public enum DevicePropertyElementType
	{
		Empty = 0x00000000,
		Null = 0x00000001,
		SByte = 0x00000002,
		Byte = 0x00000003,
		Int16 = 0x00000004,
		UInt16 = 0x00000005,
		Int32 = 0x00000006,
		UInt32 = 0x00000007,
		Int64 = 0x00000008,
		UInt64 = 0x00000009,
		Single = 0x0000000A,
		Double = 0x0000000B,
		Decimal = 0x0000000C,
		Guid = 0x0000000D,
		Currency = 0x0000000E,
		Date = 0x0000000F,
		FileTime = 0x00000010,
		Boolean = 0x00000011,
		String = 0x00000012,
		//SecurityDescriptor = 0x00000013,
		//SecurityDescriptorString = 0x00000014,
		PropertyKey = 0x00000015,
		PropertyType = 0x00000016,
		Win32Error = 0x00000017,
		NtStatus = 0x00000018,
		StringResource = 0x00000019,
	}
}
