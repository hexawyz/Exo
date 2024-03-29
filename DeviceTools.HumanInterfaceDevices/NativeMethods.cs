using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.HumanInterfaceDevices;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	private const int ErrorGenFailure = 0x0000001F;
	private const int ErrorInsufficientBuffer = 0x0000007A;
	private const int ErrorInvalidUserBuffer = 0x000006F8;
	private const int ErrorNoAccess = 0x000003E6;
	public const int ErrorNoMoreItems = 0x00000103;

	// HID IOCTL codes that can be used to access features, most of which are otherwise exposed through the HID API. (Some with more flexibility)
	// By using those codes (defined in hidclass.h), we can use asynchronous IOCTL to access the features instead of blocking code.
	// For reference:
	//   CTL_CODE(DeviceType, Function, Method, Access) => DeviceType << 16 | Access << 14 | Function << 2 | Method
	//   enum Method { Buffered, Input, Output, Neither }
	public const int IoCtlHidGetDriverConfig = 0xb0190; // (100, Buffered)
	public const int IoCtlHidSetDriverConfig = 0xb0194; // (101, Buffered)
	public const int IoCtlHidGetPollingFrequencyMillisecond = 0xb0198; // (102, Buffered)
	public const int IoCtlHidSetPollingFrequencyMillisecond = 0xb019c; // (103, Buffered)
	public const int IoCtlGetNumberOfDeviceInputBuffers = 0xb01a0; // (104, Buffered)
	public const int IoCtlSetNumberOfDeviceInputBuffers = 0xb01a4; // (105, Buffered)
	public const int IoCtlGetCollectionInformation = 0xb01a8; // (106, Buffered)
	public const int IoCtlHidEnableWakeFromSleep = 0xb01ac; // (107, Buffered)
	public const int IoCtlHidSetS0IdleTimeout = 0xb01b0; // (108, Buffered)
	public const int IoCtlGetCollectionDescriptor = 0xb0193; // (100, Neither)
	public const int IoCtlHidFlushQueue = 0xb0197; // (101, Neither)
	public const int IoCtlHidSetFeature = 0xb0191; // (100, Input)
	public const int IoCtlHidSetOutputReport = 0xb0195; // (101, Input)
	public const int IoCtlHidGetFeature = 0xb0192; // (100, Output)
	public const int IoCtlGetPhysicalDescriptor = 0xb019a; // (102, Output)
	public const int IoCtlHidGetHardwareId = 0xb019e; // (103, Output)
	public const int IoCtlHidGetInputReport = 0xb01a2; // (104, Output)
	public const int IoCtlHidGetOutputReport = 0xb01a6; // (105, Output)
	public const int IoCtlGetManufacturerString = 0xb01ba; // (110, Output)
	public const int IoCtlGetProductString = 0xb01be; // (111, Output)
	public const int IoCtlGetSerialNumberString = 0xb01c2; // (112, Output)
	public const int IoCtlGetIndexedString = 0xb01e2; // (120, Output)
	public const int IoCtlGetMsGenreDescriptor = 0xb01e6; // (121, Output)

	[StructLayout(LayoutKind.Sequential)]
	public struct HidCollectionInformation
	{
		public int DescriptorSize;
		private byte _polled;
		public bool Polled
		{
			get => _polled != 0;
			set => _polled = value ? (byte)1 : (byte)0;
		}
#pragma warning disable IDE0044
		private byte _reserved;
#pragma warning restore IDE0044
		public ushort VendorId;
		public ushort ProductId;
		public ushort VersionNumber;
	}

	//[StructLayout(LayoutKind.Sequential)]
	//public struct HidAttributes
	//{
	//	public uint Size;
	//	public ushort VendorId;
	//	public ushort ProductId;
	//	public ushort VersionNumber;
	//}

//	[StructLayout(LayoutKind.Sequential)]
//	public struct HidParsingCaps
//	{
//		public ushort Usage;
//		public HidUsagePage UsagePage;
//		public ushort InputReportByteLength;
//		public ushort OutputReportByteLength;
//		public ushort FeatureReportByteLength;
//		// There are currently 17 reserved / undocumented values in the middle of the structure.
//#pragma warning disable CS0169, IDE0051, RCS1213
//		private readonly ushort _reserved0;
//		private readonly ushort _reserved1;
//		private readonly ushort _reserved2;
//		private readonly ushort _reserved3;
//		private readonly ushort _reserved4;
//		private readonly ushort _reserved5;
//		private readonly ushort _reserved6;
//		private readonly ushort _reserved7;
//		private readonly ushort _reserved8;
//		private readonly ushort _reserved9;
//		private readonly ushort _reserved10;
//		private readonly ushort _reserved11;
//		private readonly ushort _reserved12;
//		private readonly ushort _reserved13;
//		private readonly ushort _reserved14;
//		private readonly ushort _reserved15;
//		private readonly ushort _reserved16;
//#pragma warning restore CS0169, IDE0051, RCS1213
//		public ushort LinkCollectionNodesCount;
//		public ushort InputButtonCapsCount;
//		public ushort InputValueCapsCount;
//		public ushort InputDataIndicesCount;
//		public ushort OutputButtonCapsCount;
//		public ushort OutputValueCapsCount;
//		public ushort OutputDataIndicesCount;
//		public ushort FeatureButtonCapsCount;
//		public ushort FeatureValueCapsCount;
//		public ushort FeatureDataIndicesCount;
//	}

//	// NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
//	[StructLayout(LayoutKind.Explicit)]
//	public struct HidParsingButtonCaps
//	{
//		[FieldOffset(0)]
//		public HidUsagePage UsagePage;
//		[FieldOffset(2)]
//		public byte ReportID;
//		[FieldOffset(3)]
//		public bool IsAlias;
//		[FieldOffset(4)]
//		public ushort BitField;
//		[FieldOffset(6)]
//		public ushort LinkCollection;
//		[FieldOffset(8)]
//		public ushort LinkUsage;
//		[FieldOffset(10)]
//		public HidUsagePage LinkUsagePage;
//		[FieldOffset(12)]
//		public bool IsRange;
//		[FieldOffset(13)]
//		public bool IsStringRange;
//		[FieldOffset(14)]
//		public bool IsDesignatorRange;
//		[FieldOffset(15)]
//		public bool IsAbsolute;

//		// This crucially important field was previously indicated as "Reserved"…
//		// hidpi.h says "Available in API version >= 2 only."
//		// But what is version 2 of this API ? 🤷
//		[FieldOffset(16)]
//		public ushort ReportCount;

//		// There are currently 9 reserved / undocumented uint values in the middle of the structure.

//		[FieldOffset(56)]
//		public RangeCaps Range;
//		[FieldOffset(56)]
//		public NotRangeCaps NotRange;
//	}

//	// NB: Win32 boolean are mapped to C# bool here. Provided the only two possible values of TRUE (1) and FALSE (0) are used, this should work fine.
//	[StructLayout(LayoutKind.Explicit)]
//	public struct HidParsingValueCaps
//	{
//		[FieldOffset(0)]
//		public HidUsagePage UsagePage;
//		[FieldOffset(2)]
//		public byte ReportID;
//		[FieldOffset(3)]
//		public bool IsAlias;
//		[FieldOffset(4)]
//		public ushort BitField;
//		[FieldOffset(6)]
//		public ushort LinkCollection;
//		[FieldOffset(8)]
//		public ushort LinkUsage;
//		[FieldOffset(10)]
//		public HidUsagePage LinkUsagePage;
//		[FieldOffset(12)]
//		public bool IsRange;
//		[FieldOffset(13)]
//		public bool IsStringRange;
//		[FieldOffset(14)]
//		public bool IsDesignatorRange;
//		[FieldOffset(15)]
//		public bool IsAbsolute;
//		[FieldOffset(16)]
//		public bool HasNull;

//		// There is one reserved byte for padding

//		[FieldOffset(18)]
//		public ushort BitSize;
//		[FieldOffset(20)]
//		public ushort ReportCount;

//		// There are currently 5 reserved / undocumented ushort values in the middle of the structure.

//		[FieldOffset(32)]
//		public uint UnitsExp;
//		[FieldOffset(36)]
//		public uint Units;
//		[FieldOffset(40)]
//		public int LogicalMin;
//		[FieldOffset(44)]
//		public int LogicalMax;
//		[FieldOffset(48)]
//		public int PhysicalMin;
//		[FieldOffset(52)]
//		public int PhysicalMax;

//		[FieldOffset(56)]
//		public RangeCaps Range;
//		[FieldOffset(56)]
//		public NotRangeCaps NotRange;
//	}

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
		public ushort Usage;
		private readonly ushort _reserved1;
		public ushort StringIndex;
		private readonly ushort _reserved2;
		public ushort DesignatorIndex;
		private readonly ushort _reserved3;
		public ushort DataIndex;
		private readonly ushort _reserved4;
#pragma warning restore CS0169, IDE0051, RCS1213
	}

//	public unsafe struct HidParsingExtendedAttributesHeader
//	{
//#pragma warning disable CS0169, IDE0051, RCS1213
//		public byte NumGlobalUnknowns;
//		private readonly byte _reserved1;
//		private readonly byte _reserved2;
//		private readonly byte _reserved3;
//		public HidParsingUnknownToken* GlobalUnknowns;
//#pragma warning restore CS0169, IDE0051, RCS1213
//		// Data follow this header
//	}

	public struct HidParsingUnknownToken
	{
#pragma warning disable CS0169, IDE0051, RCS1213
		public byte Token;
		private readonly byte _reserved1;
		private readonly byte _reserved2;
		private readonly byte _reserved3;
		public uint BitField;
#pragma warning restore CS0169, IDE0051, RCS1213
	}

	//public enum HidParsingResult : uint
	//{
	//	Success = 0x00110000,
	//	Null = 0x80110001,
	//	InvalidPreparsedData = 0xC0110001,
	//	InvalidReportType = 0xC0110002,
	//	InvalidReportLength = 0xC0110003,
	//	UsageNotFound = 0xC0110004,
	//	ValueOutOfRange = 0xC0110005,
	//	BadLogPhyValues = 0xC0110006,
	//	BufferTooSmall = 0xC0110007,
	//	InternalError = 0xC0110008,
	//	I8042_TRANS_UNKNOWN = 0xC0110009,
	//	IncompatibleReportId = 0xC011000A,
	//	NotValueArray = 0xC011000B,
	//	IsValueArray = 0xC011000C,
	//	DataIndexNotFound = 0xC011000D,
	//	DataIndexOutOfRange = 0xC011000E,
	//	ButtonNotPressed = 0xC011000F,
	//	ReportDoesNotExist = 0xC0110010,
	//	NotImplemented = 0xC0110020,
	//}

	public static readonly uint HidLibraryVersion = GetHidLibraryVersion();

	private static unsafe uint GetHidLibraryVersion()
	{
		uint version = 1;
		var library = NativeLibrary.Load("hid");
		try
		{
			if (NativeLibrary.TryGetExport(library, "HidP_GetVersionInternal", out nint address))
			{
				((delegate*<out uint, uint>)address)(out version);
			}
		}
		finally
		{
			NativeLibrary.Free(library);
		}

		return version;
	}

	//[DllImport("hid", EntryPoint = "HidD_GetPreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetPreparsedData(SafeFileHandle deviceFileHandle, out IntPtr preparsedData);

	//[DllImport("hid", EntryPoint = "HidD_FreePreparsedData", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryFreePreparsedData(IntPtr preparsedData);

	//[DllImport("hid", EntryPoint = "HidD_GetAttributes", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetAttributes(SafeFileHandle deviceFileHandle, ref HidAttributes attributes);

	//[DllImport("hid", EntryPoint = "HidD_GetManufacturerString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetManufacturerString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

	//[DllImport("hid", EntryPoint = "HidD_GetProductString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetProductString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

	//[DllImport("hid", EntryPoint = "HidD_GetSerialNumberString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetSerialNumberString(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

	//[DllImport("hid", EntryPoint = "HidD_GetIndexedString", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	//public static extern int HidDiscoveryGetIndexedString(SafeFileHandle deviceFileHandle, uint stringIndex, ref char firstChar, uint bufferLength);

	[DllImport("hid", EntryPoint = "HidD_GetPhysicalDescriptor", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern int HidDiscoveryGetPhysicalDescriptor(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

	[DllImport("hid", EntryPoint = "HidD_SetFeature", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern uint HidDiscoverySetFeature(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

	[DllImport("hid", EntryPoint = "HidD_GetFeature", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern uint HidDiscoveryGetFeature(SafeFileHandle deviceFileHandle, ref byte firstByte, uint bufferLength);

	//[DllImport("hid", EntryPoint = "HidP_GetCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//public static extern unsafe HidParsingResult HidParsingGetCaps(void* preparsedData, out HidParsingCaps capabilities);

	//[DllImport("hid", EntryPoint = "HidP_GetCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//public static extern HidParsingResult HidParsingGetCaps(ref byte preparsedData, out HidParsingCaps capabilities);

	//[DllImport("hid", EntryPoint = "HidP_GetButtonCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//private static extern HidParsingResult HidParsingGetButtonCaps(HidReportType reportType, ref /* HidParsingButtonCaps */ byte firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData);

	//// Work around P/Invoke refusing to consider bool as blittable…
	//public static HidParsingResult HidParsingGetButtonCaps(HidReportType reportType, ref HidParsingButtonCaps firstButtonCap, ref ushort buttonCapsLength, ref byte preparsedData)
	//	=> HidParsingGetButtonCaps(reportType, ref Unsafe.As<HidParsingButtonCaps, byte>(ref firstButtonCap), ref buttonCapsLength, ref preparsedData);

	//[DllImport("hid", EntryPoint = "HidP_GetValueCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//private static extern HidParsingResult HidParsingGetValueCaps(HidReportType reportType, ref /* HidParsingValueCaps */ byte firstValueCap, ref ushort valueCapsLength, ref byte preparsedData);

	//[DllImport("hid", EntryPoint = "HidP_GetValueCaps", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//private static extern HidParsingResult HidP_GetExtendedAttributes(HidReportType reportType, ushort dataIndex, ref byte preparsedData, ref byte attributes, ref uint attributeByteCount);

	//// Work around P/Invoke refusing to consider bool as blittable…
	//public static HidParsingResult HidParsingGetValueCaps(HidReportType reportType, ref HidParsingValueCaps firstValueCap, ref ushort buttonCapsLength, ref byte preparsedData)
	//	=> HidParsingGetValueCaps(reportType, ref Unsafe.As<HidParsingValueCaps, byte>(ref firstValueCap), ref buttonCapsLength, ref preparsedData);

	//[DllImport("hid", EntryPoint = "HidP_GetLinkCollectionNodes", ExactSpelling = true, CharSet = CharSet.Unicode)]
	//public static extern HidParsingResult HidParsingGetLinkCollectionNodes(ref HidParsingLinkCollectionNode firstNode, ref uint linkCollectionNodesLength, ref byte preparsedData);

	//public delegate int HidGetStringFunction(SafeFileHandle deviceFileHandle, ref char buffer, uint bufferLength);

	//private static readonly HidGetStringFunction HidGetManufacturerStringFunction = HidDiscoveryGetManufacturerString;
	//private static readonly HidGetStringFunction HidGetProductStringFunction = HidDiscoveryGetProductString;
	//private static readonly HidGetStringFunction HidGetSerialNumberStringFunction = HidDiscoveryGetSerialNumberString;

	//public static string GetManufacturerString(SafeFileHandle deviceHandle)
	//	=> GetHidString(deviceHandle, HidGetManufacturerStringFunction, "HidD_GetManufacturerString");

	//public static string GetProductString(SafeFileHandle deviceHandle)
	//	=> GetHidString(deviceHandle, HidGetProductStringFunction, "HidD_GetProductString");

	//public static string GetSerialNumberString(SafeFileHandle deviceHandle)
	//	=> GetHidString(deviceHandle, HidGetSerialNumberStringFunction, "HidD_GetSerialNumberString");

	//private static string GetHidString(SafeFileHandle deviceHandle, HidGetStringFunction getHidString, string functionName)
	//{
	//	int bufferLength = 256;
	//	while (true)
	//	{
	//		var buffer = ArrayPool<char>.Shared.Rent(bufferLength);
	//		buffer[0] = '\0';
	//		try
	//		{
	//			// Buffer length should not exceed 4093 bytes (so 4092 bytes because of wide chars ?)
	//			int length = Math.Min(buffer.Length, 2046);

	//			if (getHidString(deviceHandle, ref buffer[0], (uint)length * 2) == 0)
	//			{
	//				throw new Win32Exception(Marshal.GetLastWin32Error());
	//			}

	//			return buffer.AsSpan(0, length).IndexOf('\0') is int endIndex && endIndex >= 0 ?
	//				buffer.AsSpan(0, endIndex).ToString() :
	//				throw new Exception($"The string received from {functionName} was not null-terminated.");
	//		}
	//		finally
	//		{
	//			ArrayPool<char>.Shared.Return(buffer);
	//		}
	//	}
	//}

	//public static string GetIndexedString(SafeFileHandle deviceHandle, uint stringIndex)
	//	=> GetIndexedStringOnStack(deviceHandle, stringIndex) ?? GetIndexedStringInPooledArray(deviceHandle, stringIndex);

	//private static string? GetIndexedStringOnStack(SafeFileHandle deviceHandle, uint stringIndex)
	//{
	//	// From the docs: USB devices shouldn't return more than 126+1 characters. Other devices could return more.
	//	Span<char> text = stackalloc char[127];
	//	if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text.GetPinnableReference(), (uint)text.Length) == 0)
	//	{
	//		switch (Marshal.GetLastWin32Error())
	//		{
	//			case ErrorInvalidUserBuffer:
	//				return null;
	//			case int code:
	//				throw new Win32Exception(code);
	//		}
	//	}

	//	return (text.IndexOf('\0') is int endIndex && endIndex >= 0 ? text.Slice(0, endIndex) : text).ToString();
	//}

	//private static string GetIndexedStringInPooledArray(SafeFileHandle deviceHandle, uint stringIndex)
	//{
	//	const int MaxLength = 65536; // Arbitrary limit on the size we allow ourselves to request.
	//	int length = 256;

	//	// The loop will either exit by returning a valid string, or by throwing an exception.
	//	while (true)
	//	{
	//		var text = ArrayPool<char>.Shared.Rent(length);
	//		try
	//		{
	//			length = text.Length;

	//			if (HidDiscoveryGetIndexedString(deviceHandle, stringIndex, ref text[0], (uint)text.Length) != 0)
	//			{
	//				return (Array.IndexOf(text, '\0', 0) is int endIndex && endIndex >= 0 ? text.AsSpan(0, endIndex) : text.AsSpan()).ToString();
	//			}

	//			switch (Marshal.GetLastWin32Error())
	//			{
	//				case ErrorInvalidUserBuffer when length < MaxLength:
	//					length = Math.Min(2 * length, MaxLength);
	//					continue;
	//				case int code:
	//					throw new Win32Exception(code);
	//			}
	//		}
	//		finally
	//		{
	//			ArrayPool<char>.Shared.Return(text);
	//		}
	//	}
	//}

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
