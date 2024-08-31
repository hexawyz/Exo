using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using static System.Net.WebRequestMethods;
using System.Threading;

namespace DeviceTools
{
	[SuppressUnmanagedCodeSecurity]
	internal static partial class NativeMethods
	{
		private const int ErrorGenFailure = 0x0000001F;
		public const int ErrorHandleEndOfFile = 0x00000026;
		public const int ErrorBrokenPipe = 0x0000006D;
		public const int ErrorNoData = 0x000000E8;
		private const int ErrorInsufficientBuffer = 0x0000007A;
		private const int ErrorInvalidUserBuffer = 0x000006F8;
		public const int ErrorOperationAborted = 0x000003E3;
		public const int ErrorIoPending = 0x000003E5;
		private const int ErrorNoAccess = 0x000003E6;
		public const int ErrorNoMoreItems = 0x00000103;

		public static int ErrorToHResult(int errorCode) => unchecked((int)0x80070000) | errorCode;

		public const uint HResultErrorElementNotFound = 0x8002802B;

		public enum GetClassDeviceFlags
		{
			Default = 0x00000001,
			Present = 0x00000002,
			AllClasses = 0x00000004,
			Profile = 0x00000008,
			DeviceInterface = 0x00000010,
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceInfoData
		{
			public uint Size;
			public Guid ClassGuid;
			public uint DevInst;
			public UIntPtr Reserved;
		}

		public enum DeviceInterfaceFlags : uint
		{
			Active = 1,
			Default = 2,
			Removed = 3,
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceInterfaceData
		{
			public uint Size;
			public Guid InterfaceClassGuid;
			public DeviceInterfaceFlags Flags;
			public UIntPtr Reserved;
		}

		public enum ConfigurationManagerResult : uint
		{
			Success = 0x00000000,
			Default = 0x00000001,
			OutOfMemory = 0x00000002,
			InvalidPointer = 0x00000003,
			InvalidFlag = 0x00000004,
			InvalidDevNode = 0x00000005,
			InvalidDevInst = InvalidDevNode,
			InvalidResDes = 0x00000006,
			InvalidLogConf = 0x00000007,
			InvalidArbitrator = 0x00000008,
			InvalidNodeList = 0x00000009,
			DevNodeHasReqs = 0x0000000A,
			DevInstHasReqd = DevNodeHasReqs,
			InvalidResourceId = 0x0000000B,
			//DlVxdNotFound = 0x0000000C, // WIN 95 ONLY
			NoSuchDevNode = 0x0000000D,
			NoSuchDevInst = NoSuchDevNode,
			NoMoreLogConf = 0x0000000E,
			NoMoreResDes = 0x0000000F,
			AlreadySuchDevNode = 0x00000010,
			AlreadySuchDevInst = AlreadySuchDevNode,
			InvalidRangeList = 0x00000011,
			InvalidRange = 0x00000012,
			Failure = 0x00000013,
			NoSuchLogicalDev = 0x00000014,
			CreateBlocked = 0x00000015,
			//NotSystemVm = 0x00000016, // WIN 95 ONLY
			RemoveVetoed = 0x00000017,
			ApmVetoed = 0x00000018,
			InvalidLoadType = 0x00000019,
			BufferSmall = 0x0000001A,
			NoArbitrator = 0x0000001B,
			NoRegistryHandle = 0x0000001C,
			RegistryError = 0x0000001D,
			InvalidDeviceId = 0x0000001E,
			InvalidData = 0x0000001F,
			InvalidApi = 0x00000020,
			DevLoaderNotReady = 0x00000021,
			NeedRestart = 0x00000022,
			NoMoreHwProfiles = 0x00000023,
			DeviceNotThere = 0x00000024,
			NoSuchValue = 0x00000025,
			WrongType = 0x00000026,
			InvalidPriority = 0x00000027,
			NotDisableable = 0x00000028,
			FreeResources = 0x00000029,
			QueryVetoed = 0x0000002A,
			CantShareIrq = 0x0000002B,
			NoDependent = 0x0000002C,
			SameResources = 0x0000002D,
			NoSuchRegistryKey = 0x0000002E,
			//InvalidMachineName = 0x0000002F, // NT ONLY
			//RemoteCommFailure = 0x00000030, // NT ONLY
			//MachineUnavailable = 0x00000031, // NT ONLY
			//NoCmServices = 0x00000032, // NT ONLY
			//AccessDenied = 0x00000033, // NT ONLY
			CallNotImplemented = 0x00000034,
			InvalidProperty = 0x00000035,
			DeviceInterfaceActive = 0x00000036,
			NoSuchDeviceInterface = 0x00000037,
			InvalidReferenceString = 0x00000038,
			InvalidConflictList = 0x00000039,
			InvalidIndex = 0x0000003A,
			InvalidStructureSize = 0x0000003B,
		}

		public enum GetDeviceIdListFlags : uint
		{
			FilterNone = 0x00000000,
			FilterEnumerator = 0x00000001,
			FilterService = 0x00000002,
			FilterEjectRelations = 0x00000004,
			FilterRemovalRelations = 0x00000008,
			FilterPowerRelations = 0x00000010,
			FilterBusRelations = 0x00000020,
			DoNotGenerate = 0x10000040,
			FilterTransportRelations = 0x00000080,
			FilterPresent = 0x00000100,
			FilterClass = 0x00000200,
		}

		public enum GetDeviceInterfaceListFlags : uint
		{
			Present = 0,
			All = 1,
		}

		[Flags]
		public enum FileAccessMask : uint
		{
			FileListDirectory = 0x00000001,
			FileReadData = 0x00000001,
			FileWriteData = 0x00000002,
			FileAddFile = 0x00000002,
			FileAddSubdirectory = 0x00000004,
			FileAppendData = 0x00000004,
			FileCreatePipeInstance = 0x00000004,
			FileReadExtendedAttributes = 0x00000008,
			FileWriteExtendedAttributes = 0x00000010,
			FileExecute = 0x00000020,
			FileTraverse = 0x00000020,
			FileDeleteChild = 0x00000040,
			FileReadAttributes = 0x00000080,
			FileWriteAttributes = 0x00000100,
			Delete = 0x00010000,
			ReadControl = 0x00020000,
			WriteDac = 0x00040000,
			WriteOwner = 0x00080000,
			Synchronize = 0x00100000,
			StandardRightsRequired = 0x000F0000,
			StandardRightsRead = ReadControl,
			StandardRightsWrite = ReadControl,
			StandardRightsExecute = ReadControl,
			AccessSystemSecurity = 0x01000000,
			MaximumAllowed = 0x02000000,
			GenericAll = 0x10000000,
			GenericExecute = 0x20000000,
			GenericWrite = 0x40000000,
			GenericRead = 0x80000000,
			FileAllAccess = StandardRightsRequired | Synchronize | 0x1FF,
			FileGenericRead = StandardRightsRead | FileReadData | FileReadAttributes | FileReadExtendedAttributes | Synchronize,
			FileGenericWrite = StandardRightsWrite | FileWriteData | FileWriteAttributes | FileWriteExtendedAttributes | FileAppendData | Synchronize,
			FileGenericExecute = StandardRightsExecute | FileReadAttributes | FileExecute | Synchronize,
		}

		// Not really a flags enum, but can combine some values.
		public enum DevicePropertyType
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
			Float = 0x0000000A,
			Double = 0x0000000B,
			Decimal = 0x0000000C,
			Guid = 0x0000000D,
			Currency = 0x0000000E,
			Date = 0x0000000F,
			FileTime = 0x00000010,
			Boolean = 0x00000011,
			String = 0x00000012,
			SecurityDescriptor = 0x00000013,
			SecurityDescriptorString = 0x00000014,
			DevicePropertyKey = 0x00000015,
			DevicePropertyType = 0x00000016,
			Error = 0x00000017,
			NtStatus = 0x00000018,
			StringResource = 0x00000019,

			Array = 0x00001000,
			List = 0x00002000,

			StringList = String | List,
			Binary = Byte | Array,

			MaskType = 0x00000FFF,
			MaskTypeModifier = 0x0000F000,
		}

		public enum VarType : ushort
		{
			Empty = 0,
			Null = 1,
			SByte = 16,
			Byte = 17,
			Int16 = 2,
			UInt16 = 18,
			Int32 = 3,
			UInt32 = 19,
			Int64 = 20,
			UInt64 = 21,
			Float = 4,
			Double = 5,
			Boolean = 11,
			Error = 10,
			Currency = 6,
			Date = 7,
			FileTime = 64,
			Guid = 72,
			ClipboardData = 71,
			BStr = 8,
			BStrBlob = 0xfff,
			Blob = 65,
			BlobObject = 70,
			AwsiString = 30,
			UnicodeString = 31,
			IUnknown = 13,
			IDispatch = 9,
			Stream = 66,
			StreamedObject = 68,
			Storage = 67,
			StoredObject = 69,
			VersionedStream = 73,
			Decimal = 14,

			Vector = 0x1000,
			Array = 0x2000,
			ByRef = 0x4000,

			Variant = 12,
		}

		[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
		public struct PropertyVariant
		{
			[FieldOffset(0)]
			public VarType VarType;
			[FieldOffset(2)]
			public ushort Reserved1;
			[FieldOffset(4)]
			public ushort Reserved2;
			[FieldOffset(6)]
			public ushort Reserved3;
			[FieldOffset(8)]
			public byte Byte;
			[FieldOffset(8)]
			public sbyte SByte;
			[FieldOffset(8)]
			public short Int16;
			[FieldOffset(8)]
			public ushort UInt16;
			[FieldOffset(8)]
			public int Int32;
			[FieldOffset(8)]
			public uint UInt32;
			[FieldOffset(8)]
			public long Int64;
			[FieldOffset(8)]
			public ulong UInt64;
			[FieldOffset(8)]
			public float Float;
			[FieldOffset(8)]
			public double Double;
			[FieldOffset(8)]
			public IntPtr IntPtr;
			[FieldOffset(16)]
			public IntPtr IntPtr2;
		}

		public static class ShellPropertyCategoryGuids
		{
			// Names of those Guids were mostly reverse-engineered. Some of them may be wrong, but it is close to impossible to find a good source.
			public static readonly Guid Storage = new(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac);
			public static readonly Guid Device = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0);
			public static readonly Guid DeviceOther = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2);
			public static readonly Guid DeviceDriver = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6);
			public static readonly Guid DeviceClass = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66);
			public static readonly Guid DeviceInterface = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22);
			public static readonly Guid DeviceInterfaceClassGuid = new(0xcbf38310, 0x4a17, 0x4310, 0xa1, 0xeb, 0x24, 0x7f, 0xb, 0x67, 0x59, 0x3b);
			public static readonly Guid DeviceInterfaceClass = new(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64);
			public static readonly Guid DeviceContainer = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57);
			public static readonly Guid DeviceContainer2 = new(0x656a3bb3, 0xecc0, 0x43fd, 0x84, 0x77, 0x4a, 0xe0, 0x40, 0x4a, 0x96, 0xcd);
			public static readonly Guid DeviceContainerMapping = new(0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c);
			public static readonly Guid Bluetooth = new(0x2bd67d8b, 0x8beb, 0x48d5, 0x87, 0xe0, 0x6c, 0xda, 0x34, 0x28, 0x04, 0x0a);
			public static readonly Guid Serial = new(0x4c6bf15c, 0x4c03, 0x4aac, 0x91, 0xf5, 0x64, 0xc0, 0xf8, 0x52, 0xbc, 0xf4);
			public static readonly Guid WinUsb = new(0x95e127b5, 0x79cc, 0x4e83, 0x9c, 0x9e, 0x84, 0x22, 0x18, 0x7b, 0x3e, 0x0e);
			public static readonly Guid Smartphone = new(0x49cd1f76, 0x5626, 0x4b17, 0xa4, 0xe8, 0x18, 0xb4, 0xaa, 0x1a, 0x22, 0x13);
		}

		public static class DevicePropertyKeys
		{
			public static readonly PropertyKey DeviceInterfaceFriendlyName = new(ShellPropertyCategoryGuids.DeviceInterface, 2);
			public static readonly PropertyKey DeviceInterfaceEnabled = new(ShellPropertyCategoryGuids.DeviceInterface, 3);
			public static readonly PropertyKey DeviceInterfaceClassGuid = new(ShellPropertyCategoryGuids.DeviceInterface, 4);

			public static readonly PropertyKey DeviceModel = new(ShellPropertyCategoryGuids.DeviceContainer, 39);
			// For a relatively old list of categories, see: https://learn.microsoft.com/en-us/previous-versions/windows/hardware/metadata/dn465876%28v=vs.85%29
			public static readonly PropertyKey DeviceContainerCategoryIcon = new(ShellPropertyCategoryGuids.DeviceContainer, 93);
			public static readonly PropertyKey DeviceContainerPrimaryCategory = new(ShellPropertyCategoryGuids.DeviceContainer, 97);

			public static readonly PropertyKey DeviceContainerModelName = new(ShellPropertyCategoryGuids.DeviceContainer2, 8194);

			public static readonly PropertyKey InLocalMachineContainer = new(ShellPropertyCategoryGuids.DeviceContainerMapping, 4);
		}

		public enum LocateDeviceNodeFlags
		{
			Normal = 0x00000000,
			Phantom = 0x00000001,
			CancelRemove = 0x00000002,
			NoValidation = 0x00000004,
		}

		[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[ComImport]
		public interface IPropertyStore
		{
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			[return: MarshalAs(UnmanagedType.U4)]
			uint GetCount();
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			PropertyKey GetAt(uint iProp);
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			void GetValue(in PropertyKey key, ref PropertyVariant propertyVariant);
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			void SetValue(in PropertyKey key, in PropertyVariant propertyVariant);
			[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
			void Commit();
		}

		[Flags]
		public enum GetPropertyStoreFlags
		{
			Default = 0x0,
			HandlerPropertiesOnly = 0x1,
			ReadWrite = 0x2,
			Temporary = 0x4,
			FastPropertiesOnly = 0x8,
			OpensLowItem = 0x10,
			DelayCreation = 0x20,
			BestEffort = 0x40,
			NoOpportunisticLock = 0x80,
			PreferQueryProperties = 0x100,
			ExtrinsicProperties = 0x200,
			ExtrinsicPropertiesOnly = 0x400,
			VolatileProperties = 0x800,
			VolatilePropertiesOnly = 0x1000,
		}

		[DllImport("SetupAPI", EntryPoint = "SetupDiGetClassDevsW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeDeviceInfoListHandle SetupDiGetClassDevs(in Guid classGuid, string enumerator, IntPtr hwndParent, GetClassDeviceFlags flags);

		[DllImport("SetupAPI", EntryPoint = "SetupDiGetClassDevsW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeDeviceInfoListHandle SetupDiGetClassDevs(in Guid classGuid, IntPtr zero, IntPtr hwndParent, GetClassDeviceFlags flags);

		[DllImport("SetupAPI", EntryPoint = "SetupDiDestroyDeviceInfoList", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

		[DllImport("SetupAPI", EntryPoint = "SetupDiEnumDeviceInterfaces", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiEnumDeviceInterfaces
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			ref DeviceInfoData deviceInfoData,
			in Guid interfaceClassGuid,
			uint memberIndex,
			ref DeviceInterfaceData deviceInterfaceData
		);

		[DllImport("SetupAPI", EntryPoint = "SetupDiEnumDeviceInterfaces", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiEnumDeviceInterfaces
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			IntPtr zero,
			in Guid interfaceClassGuid,
			uint memberIndex,
			ref DeviceInterfaceData deviceInterfaceData
		);

		[DllImport("SetupAPI", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiGetDeviceInterfaceDetail
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			ref DeviceInterfaceData deviceInterfaceData,
			IntPtr zero1,
			uint deviceInterfaceDetailDataSize,
			out uint requiredSize,
			IntPtr zero2
		);

		[DllImport("SetupAPI", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiGetDeviceInterfaceDetail
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			ref DeviceInterfaceData deviceInterfaceData,
			ref uint deviceInterfaceDetailData, // uint Size + char[] Data
			uint deviceInterfaceDetailDataSize,
			out uint requiredSize,
			ref DeviceInfoData deviceInfoData
		);

		[DllImport("SetupAPI", EntryPoint = "SetupDiGetDeviceInterfaceDetailW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiGetDeviceInterfaceDetail
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			ref DeviceInterfaceData deviceInterfaceData,
			ref uint deviceInterfaceDetailData, // uint Size + char[] Data
			uint deviceInterfaceDetailDataSize,
			out uint requiredSize,
			IntPtr zero
		);

		public static string SetupDiGetDeviceInterfaceDetail(SafeDeviceInfoListHandle handle, ref NativeMethods.DeviceInterfaceData interfaceData)
		{
			// Gets the exact length required for the buffer.
			// Sadly, because of the quite stupid requirement of the useless "Size" field of interface detail data, we can't just allocate a tring from this 😡
			if (NativeMethods.SetupDiGetDeviceInterfaceDetail(handle, ref interfaceData, IntPtr.Zero, 0, out uint requiredTotalSize, IntPtr.Zero) == 0)
			{
				int errorCode = Marshal.GetLastWin32Error();

				if (errorCode != ErrorInsufficientBuffer)
				{
					throw new Win32Exception(errorCode);
				}
			}

			// Get a temporary buffer and retrieve that length.
			var buffer = ArrayPool<byte>.Shared.Rent(checked((int)requiredTotalSize));
			try
			{
				// The buffer represents a variable length struct (SP_DeviceInterfaceDETAIL_DATA) whith only one fixed length field:
				// DWORD cbSize;
				// CHAR DevicePath[ANYSIZE_ARRAY];
				ref uint size = ref Unsafe.As<byte, uint>(ref buffer[0]);
				size = IntPtr.Size == 4 ? 6U : 8U; // sizeof(SP_DeviceInterfaceDETAIL_DATA) in C accounts for the padding between cbSize and DevicePath.
				if (NativeMethods.SetupDiGetDeviceInterfaceDetail(handle, ref interfaceData, ref size, (uint)buffer.Length, out requiredTotalSize, IntPtr.Zero) == 0)
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}

				// Skip the fixed part of the data structure and get the path.
				return MemoryMarshal.Cast<byte, char>(buffer.AsSpan(4, (int)(requiredTotalSize) - 4)).ToString();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(buffer);
			}
		}

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_ID_ListW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceIdList(in char filter, ref char buffer, uint bufferLength, GetDeviceIdListFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_ID_List_SizeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceIdListSize(out uint length, in char filter, GetDeviceIdListFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_Interface_ListW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceList(in Guid interfaceClassGuid, [In] string? deviceInstanceId, ref char buffer, uint bufferLength, GetDeviceInterfaceListFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_Interface_List_SizeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceListSize(out uint length, in Guid interfaceClassGuid, [In] string? deviceInstanceId, GetDeviceInterfaceListFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Locate_DevNodeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerLocateDeviceNode(out uint deviceInstanceHandle, [In] string? deviceInstanceId, LocateDeviceNodeFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_Interface_PropertyW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceProperty([In] string? deviceInterfaceId, in PropertyKey propertyKey, out DevicePropertyType propertyType, ref byte buffer, ref uint bufferSize, uint flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_DevNode_PropertyW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceNodeProperty(uint deviceInstanceHandle, in PropertyKey propertyKey, out DevicePropertyType propertyType, ref byte buffer, ref uint bufferSize, uint flags);

		[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
		public static unsafe extern uint DeviceIoControl
		(
			SafeFileHandle deviceHandle,
			uint ioControlCode,
			in byte inputBufferFirstByte,
			uint inputBufferSize,
			ref byte outputBufferFirstByte,
			uint outputBufferSize,
			out uint bytesReturned,
			NativeOverlapped *overlapped
		);

		[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
		public static unsafe extern uint DeviceIoControl
		(
			SafeFileHandle deviceHandle,
			uint ioControlCode,
			byte* inputBufferFirstByte,
			uint inputBufferSize,
			byte* outputBufferFirstByte,
			uint outputBufferSize,
			uint* bytesReturned,
			NativeOverlapped* overlapped
		);

		[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
		public static unsafe extern uint CancelIoEx(SafeFileHandle fileHandle, NativeOverlapped* overlapped);

#if !NET8_0_OR_GREATER
		[DllImport("kernel32", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeFileHandle CreateFile(string fileName, FileAccessMask desiredAccess, FileShare shareMode, IntPtr securityAttributes, FileMode creationDisposition, FileOptions dwFlagsAndAttributes, IntPtr hTemplateFile);
#endif

		[DllImport("shell32", EntryPoint = "SHGetPropertyStoreFromParsingName", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		[return: MarshalAs(UnmanagedType.Interface)]
		public static extern IPropertyStore SHGetPropertyStoreFromParsingName
		(
			[In] string pszPath,
			[MarshalAs(UnmanagedType.IUnknown)] object? bindContext,
			GetPropertyStoreFlags flags,
			in Guid riid
		);

		[DllImport("ole32", EntryPoint = "PropVariantClear", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void PropVariantClear(in PropertyVariant variant);

		[DllImport("propsys", EntryPoint = "PSGetNameFromPropertyKey", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern uint PSGetNameFromPropertyKey(in PropertyKey propertyKey, out IntPtr canonicalName);
	}
}
