using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.HumanInterfaceDevices
{
	[SuppressUnmanagedCodeSecurity]
	public static class NativeMethods
	{
		private const int ErrorGenFailure = 0x0000001F;
		private const int ErrorInsufficientBuffer = 0x0000007A;
		private const int ErrorInvalidUserBuffer = 0x000006F8;
		private const int ErrorNoAccess = 0x000003E6;
		public const int ErrorNoMoreItems = 0x00000103;

		[StructLayout(LayoutKind.Sequential)]
		public struct HidParsingCaps
		{
			public ushort Usage;
			public HidUsagePage UsagePage;
			public ushort InputReportByteLength;
			public ushort OutputReportByteLength;
			public ushort FeatureReportByteLength;
			// There are currently 17 reserved / undocumented values in the middle of the structure.
#pragma warning disable CS0169, IDE0051, RCS1213
			private readonly ushort _reserved0;
			private readonly ushort _reserved1;
			private readonly ushort _reserved2;
			private readonly ushort _reserved3;
			private readonly ushort _reserved4;
			private readonly ushort _reserved5;
			private readonly ushort _reserved6;
			private readonly ushort _reserved7;
			private readonly ushort _reserved8;
			private readonly ushort _reserved9;
			private readonly ushort _reserved10;
			private readonly ushort _reserved11;
			private readonly ushort _reserved12;
			private readonly ushort _reserved13;
			private readonly ushort _reserved14;
			private readonly ushort _reserved15;
			private readonly ushort _reserved16;
#pragma warning restore CS0169, IDE0051, RCS1213
			public ushort LinkCollectionNodesCount;
			public ushort InputButtonCapsCount;
			public ushort InputValueCapsCount;
			public ushort InputDataIndicesCount;
			public ushort OutputButtonCapsCount;
			public ushort OutputValueCapsCount;
			public ushort OutputDataIndicesCount;
			public ushort FeatureButtonCapsCount;
			public ushort FeatureValueCapsCount;
			public ushort FeatureDataIndicesCount;
		}

		// NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
		[StructLayout(LayoutKind.Explicit)]
		public struct HidParsingButtonCaps
		{
			[FieldOffset(0)]
			public HidUsagePage UsagePage;
			[FieldOffset(2)]
			public byte ReportID;
			[FieldOffset(3)]
			public bool IsAlias;
			[FieldOffset(4)]
			public ushort BitField;
			[FieldOffset(6)]
			public ushort LinkCollection;
			[FieldOffset(8)]
			public ushort LinkUsage;
			[FieldOffset(10)]
			public HidUsagePage LinkUsagePage;
			[FieldOffset(12)]
			public bool IsRange;
			[FieldOffset(13)]
			public bool IsStringRange;
			[FieldOffset(14)]
			public bool IsDesignatorRange;
			[FieldOffset(15)]
			public bool IsAbsolute;

			// There are currently 10 reserved / undocumented uint values in the middle of the structure.

			[FieldOffset(56)]
			public RangeCaps Range;
			[FieldOffset(56)]
			public NotRangeCaps NotRange;
		}

		// NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
		[StructLayout(LayoutKind.Explicit)]
		public struct HidParsingValueCaps
		{
			[FieldOffset(0)]
			public HidUsagePage UsagePage;
			[FieldOffset(2)]
			public byte ReportID;
			[FieldOffset(3)]
			public bool IsAlias;
			[FieldOffset(4)]
			public ushort BitField;
			[FieldOffset(6)]
			public ushort LinkCollection;
			[FieldOffset(8)]
			public ushort LinkUsage;
			[FieldOffset(10)]
			public HidUsagePage LinkUsagePage;
			[FieldOffset(12)]
			public bool IsRange;
			[FieldOffset(13)]
			public bool IsStringRange;
			[FieldOffset(14)]
			public bool IsDesignatorRange;
			[FieldOffset(15)]
			public bool IsAbsolute;
			[FieldOffset(16)]
			public bool HasNull;

			// There is one reserved byte for padding

			[FieldOffset(18)]
			public ushort BitSize;
			[FieldOffset(20)]
			public ushort ReportCount;

			// There are currently 5 reserved / undocumented ushort values in the middle of the structure.

			[FieldOffset(32)]
			public uint UnitsExp;
			[FieldOffset(36)]
			public uint Units;
			[FieldOffset(40)]
			public int LogicalMin;
			[FieldOffset(44)]
			public int LogicalMax;
			[FieldOffset(48)]
			public int PhysicalMin;
			[FieldOffset(52)]
			public int PhysicalMax;

			[FieldOffset(56)]
			public RangeCaps Range;
			[FieldOffset(56)]
			public NotRangeCaps NotRange;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RangeCaps
		{
			public ushort UsageMin;
			public ushort UsageMax;
			public ushort StringMin;
			public ushort StringMax;
			public ushort DesignatorMin;
			public ushort DesignatorMax;
			public ushort DataIndexMin;
			public ushort DataIndexMax;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct NotRangeCaps
		{
			// A few fields are left unused compared to the alternative Range structure.
#pragma warning disable CS0169, IDE0051, RCS1213
			private readonly ushort _reserved1;
			public ushort Usage;
			public ushort StringIndex;
			private readonly ushort _reserved2;
			public ushort DesignatorIndex;
			private readonly ushort _reserved3;
			public ushort DataIndex;
			private readonly ushort _reserved4;
#pragma warning restore CS0169, IDE0051, RCS1213
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct HidParsingLinkCollectionNode
		{
			private static readonly byte IsAliasMask = BitConverter.IsLittleEndian ? (byte)0x01 : (byte)0x80;

			public ushort LinkUsage;
			public HidUsagePage LinkUsagePage;
			public ushort Parent;
			public ushort ChildCount;
			public ushort NextSibling;
			public ushort FirstChild;

			// The following fields are declared as C++ bitfields, which means that the layout slightly differs between little & big endian… great ! 😣
			// NB: The bitfield should be aligned as an uint. (The sequential layout is already sufficently packed in that regard)
			public HidCollectionType CollectionType; // The first byte-sized field should be mostly unaffected.
			private byte _isAlias; // However, this following one bit-sized field would be 0x80 on big endian, and 0x01 on little endian.
			private readonly ushort _reserved; // Thankfully, nothing else is used for now…

			public bool IsAlias
			{
				get => (_isAlias & IsAliasMask) != 0;
				set => _isAlias = value ? (byte)(_isAlias | IsAliasMask) : (byte)(_isAlias & ~IsAliasMask);
			}

			public IntPtr UserContext;
		}

		public enum HidParsingReportType
		{
			Input = 0,
			Output = 1,
			Feature = 2,
		}

		public enum HidParsingResult : uint
		{
			Success = 0x00110000,
			Null = 0x80110001,
			InvalidPreparsedData = 0xC0110001,
			InvalidReportType = 0xC0110002,
			InvalidReportLength = 0xC0110003,
			UsageNotFound = 0xC0110004,
			ValueOutOfRange = 0xC0110005,
			BadLogPhyValues = 0xC0110006,
			BufferTooSmall = 0xC0110007,
			InternalError = 0xC0110008,
			I8042_TRANS_UNKNOWN = 0xC0110009,
			IncompatibleReportId = 0xC011000A,
			NotValueArray = 0xC011000B,
			IsValueArray = 0xC011000C,
			DataIndexNotFound = 0xC011000D,
			DataIndexOutOfRange = 0xC011000E,
			ButtonNotPressed = 0xC011000F,
			ReportDoesNotExist = 0xC0110010,
			NotImplemented = 0xC0110020,
		}

		[DllImport("hid", EntryPoint = "HidD_GetPreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetPreparsedData(SafeFileHandle deviceFileHandle, out IntPtr preparsedData);

		[DllImport("hid", EntryPoint = "HidD_FreePreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryFreePreparsedData(IntPtr preparsedData);

		[DllImport("hid", EntryPoint = "HidD_GetManufacturerString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetManufacturerString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_GetProductString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetProductString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_GetSerialNumberString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetSerialNumberString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_GetIndexedString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetIndexedString(SafeFileHandle deviceFileHandle, uint stringIndex, ref char firstChar, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_GetPhysicalDescriptor", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int HidDiscoveryGetPhysicalDescriptor(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_SetFeature", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern uint HidDiscoverySetFeature(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidD_GetFeature", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern uint HidDiscoveryGetFeature(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

		[DllImport("hid", EntryPoint = "HidP_GetCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern HidParsingResult HidParsingGetCaps(ref byte preparsedData, out HidParsingCaps capabilities);

		[DllImport("hid", EntryPoint = "HidP_GetButtonCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
		private static extern HidParsingResult HidParsingGetButtonCaps(HidParsingReportType reportType, ref /* HidParsingButtonCaps */ byte firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData);

		// Work around P/Invoke refusing to consider bool as blittable…
		public static HidParsingResult HidParsingGetButtonCaps(HidParsingReportType reportType, ref HidParsingButtonCaps firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData)
			=> HidParsingGetButtonCaps(reportType, ref Unsafe.As<HidParsingButtonCaps, byte>(ref firstButtonCap), ref buttonCapsLength, ref preparsedData);

		[DllImport("hid", EntryPoint = "HidP_GetValueCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
		private static extern HidParsingResult HidParsingGetValueCaps(HidParsingReportType reportType, ref /* HidParsingValueCaps */ byte firstValueCap, ref ushort valueCapsLength, ref byte preparsedData);

		// Work around P/Invoke refusing to consider bool as blittable…
		public static HidParsingResult HidParsingGetValueCaps(HidParsingReportType reportType, ref HidParsingValueCaps firstValueCap, ref ushort buttonCapsLength, ref byte preparsedData)
			=> HidParsingGetValueCaps(reportType, ref Unsafe.As<HidParsingValueCaps, byte>(ref firstValueCap), ref buttonCapsLength, ref preparsedData);

		[DllImport("hid", EntryPoint = "HidP_GetLinkCollectionNodes", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern HidParsingResult HidParsingGetLinkCollectionNodes(ref HidParsingLinkCollectionNode firstNode, ref uint linkCollectionNodesLength, ref byte preparsedData);

		public delegate int HidGetStringFunction(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

		private static readonly HidGetStringFunction HidGetManufacturerStringFunction = HidDiscoveryGetManufacturerString;
		private static readonly HidGetStringFunction HidGetProductStringFunction = HidDiscoveryGetProductString;
		private static readonly HidGetStringFunction HidGetSerialNumberStringFunction = HidDiscoveryGetSerialNumberString;

		public static string GetManufacturerString(SafeFileHandle deviceHandle)
			=> GetHidString(deviceHandle, HidGetManufacturerStringFunction, "HidD_GetManufacturerString");

		public static string GetProductString(SafeFileHandle deviceHandle)
			=> GetHidString(deviceHandle, HidGetProductStringFunction, "HidD_GetProductString");

		public static string GetSerialNumberString(SafeFileHandle deviceHandle)
			=> GetHidString(deviceHandle, HidGetSerialNumberStringFunction, "HidD_GetSerialNumberString");

		private static string GetHidString(SafeFileHandle deviceHandle, HidGetStringFunction getHidString, string functionName)
		{
			int bufferLength = 256;
			while (true)
			{
				var buffer = ArrayPool<char>.Shared.Rent(bufferLength);
				buffer[0] = '\0';
				try
				{
					// Buffer length should not exceed 4093 bytes (so 4092 bytes because of wide chars ?)
					int length = Math.Min(buffer.Length, 2046);

					if (getHidString(deviceHandle, ref buffer[0], (uint)length * 2) == 0)
					{
						throw new Win32Exception(Marshal.GetLastWin32Error());
					}

					return buffer.AsSpan(0, length).IndexOf('\0') is int endIndex && endIndex >= 0 ?
						buffer.AsSpan(0, endIndex).ToString() :
						throw new Exception($"The string received from {functionName} was not null-terminated.");
				}
				finally
				{
					ArrayPool<char>.Shared.Return(buffer);
				}
			}
		}

		public static string GetIndexedString(SafeFileHandle deviceHandle, uint stringIndex)
			=> GetIndexedStringOnStack(deviceHandle, stringIndex) ?? GetIndexedStringInPooledArray(deviceHandle, stringIndex);

		private static string? GetIndexedStringOnStack(SafeFileHandle deviceHandle, uint stringIndex)
		{
			// From the docs: USB devices shouldn't return more than 126+1 characters. Other devices could return more.
			Span<char> text = stackalloc char[127];
			if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text.GetPinnableReference(), (uint)text.Length) == 0)
			{
				switch (Marshal.GetLastWin32Error())
				{
					case ErrorInvalidUserBuffer:
						return null;
					case int code:
						throw new Win32Exception(code);
				}
			}

			return (text.IndexOf('\0') is int endIndex && endIndex >= 0 ? text.Slice(0, endIndex) : text).ToString();
		}

		private static string GetIndexedStringInPooledArray(SafeFileHandle deviceHandle, uint stringIndex)
		{
			const int MaxLength = 65536; // Arbitrary limit on the size we allow ourselves to request.
			int length = 256;

			// The loop will either exit by returning a valid string, or by throwing an exception.
			while (true)
			{
				var text = ArrayPool<char>.Shared.Rent(length);
				try
				{
					length = text.Length;

					if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text[0], (uint)text.Length) != 0)
					{
						return (Array.IndexOf(text, '\0', 0) is int endIndex && endIndex >= 0 ? text.AsSpan(0, endIndex) : text.AsSpan()).ToString();
					}

					switch (Marshal.GetLastWin32Error())
					{
						case ErrorInvalidUserBuffer when length < MaxLength:
							length = Math.Min(2 * length, MaxLength);
							continue;
						case int code:
							throw new Win32Exception(code);
					}
				}
				finally
				{
					ArrayPool<char>.Shared.Return(text);
				}
			}
		}

		public static PhysicalDescriptorSetCollection GetPhysicalDescriptor(SafeFileHandle deviceHandle)
			=> GetPhysicalDescriptorOnStack(deviceHandle) ?? GetPhysicalDescriptorInPooledArray(deviceHandle);

		private static PhysicalDescriptorSetCollection? GetPhysicalDescriptorOnStack(SafeFileHandle deviceHandle)
		{
			Span<byte> data = stackalloc byte[256];
			if (HidDiscoveryGetPhysicalDescriptor(deviceHandle, ref data.GetPinnableReference(), (uint)data.Length) == 0)
			{
				switch (Marshal.GetLastWin32Error())
				{
					// Assume that ERROR_GEN_FAILURE means that the device has no physical descriptor. (Should be the most common case)
					// Not sure yet what is the cause of ERROR_NO_ACCESS, but it might be returned for buggy devices.
					case ErrorGenFailure:
					case ErrorNoAccess:
						return PhysicalDescriptorSetCollection.Empty;
					case ErrorInvalidUserBuffer:
						return null;
					case int code:
						throw new Win32Exception(code);
				}
			}

			return new PhysicalDescriptorSetCollection(data, false);
		}

		private static PhysicalDescriptorSetCollection GetPhysicalDescriptorInPooledArray(SafeFileHandle deviceHandle)
		{
			// The absolute maximum data length of the physical descriptor set collection is around 32 MB (With 255 descriptor sets of 65535 descriptors each)
			const int MaxLength = 3 + 255 * (1 + 2 * 65535);
			// Start with a 4k buffer, which should hopefully be a reasonable size for most physical descriptors.
			int length = 4096;

			// The loop will either exit by returning a valid collection, or by throwing an exception.
			while (true)
			{
				var data = ArrayPool<byte>.Shared.Rent(length);
				try
				{
					length = data.Length;

					if (HidDiscoveryGetPhysicalDescriptor(deviceHandle, ref data[0], (uint)data.Length) != 0)
					{
						return new PhysicalDescriptorSetCollection(data, false);
					}

					switch (Marshal.GetLastWin32Error())
					{
						// Assume that ERROR_GEN_FAILURE means that the device has no physical descriptor. (Should be the most common case)
						// Not sure yet what is the cause of ERROR_NO_ACCESS, but it might be returned for buggy devices.
						case ErrorGenFailure:
						case ErrorNoAccess:
							return PhysicalDescriptorSetCollection.Empty;
						case ErrorInvalidUserBuffer when length < MaxLength:
							length = Math.Min(2 * length, MaxLength);
							continue;
						case int code:
							throw new Win32Exception(code);
					}
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(data);
				}
			}
		}
	}
}
