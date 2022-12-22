using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace DeviceTools
{
	[SuppressUnmanagedCodeSecurity]
	internal static class NativeMethods
	{
		private const int ErrorGenFailure = 0x0000001F;
		private const int ErrorInsufficientBuffer = 0x0000007A;
		private const int ErrorInvalidUserBuffer = 0x000006F8;
		private const int ErrorNoAccess = 0x000003E6;
		public const int ErrorNoMoreItems = 0x00000103;

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

		[StructLayout(LayoutKind.Sequential)]
		public readonly struct DevicePropertyKey
		{
			public readonly Guid CategoryId;
			public readonly uint PropertyId;

			public DevicePropertyKey(Guid categoryId, uint propertyId)
			{
				CategoryId = categoryId;
				PropertyId = propertyId;
			}
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
		}

		public static class ShellPropertyGuids
		{
			// Names of those Guids were mostly reverse-engineered. Some of them may be wrong.
			public static readonly Guid Storage = new(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac);
			public static readonly Guid Device = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0);
			public static readonly Guid DeviceDriver = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6);
			public static readonly Guid DeviceClass = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66);
			public static readonly Guid DeviceInterface = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22);
			public static readonly Guid DeviceInterfaceClass = new(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64);
			public static readonly Guid DeviceContainer = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57);
		}

		public static class DevicePropertyKeys
		{
			public static readonly DevicePropertyKey ItemNameDisplay = new(ShellPropertyGuids.Storage, 10);

			public static readonly DevicePropertyKey DeviceDescription = new(ShellPropertyGuids.Device, 2);
			public static readonly DevicePropertyKey DeviceHardwareIds = new(ShellPropertyGuids.Device, 3);
			public static readonly DevicePropertyKey DeviceCompatibleIds = new(ShellPropertyGuids.Device, 4);
			public static readonly DevicePropertyKey DeviceService = new(ShellPropertyGuids.Device, 6);
			public static readonly DevicePropertyKey DeviceClass = new(ShellPropertyGuids.Device, 9);
			public static readonly DevicePropertyKey DeviceClassGuid = new(ShellPropertyGuids.Device, 10);
			public static readonly DevicePropertyKey DeviceDriver = new(ShellPropertyGuids.Device, 11);
			public static readonly DevicePropertyKey DeviceConfigFlags = new(ShellPropertyGuids.Device, 12);
			public static readonly DevicePropertyKey DeviceManufacturer = new(ShellPropertyGuids.Device, 13);
			public static readonly DevicePropertyKey DeviceFriendlyName = new(ShellPropertyGuids.Device, 14);
			public static readonly DevicePropertyKey DeviceLocationInfo = new(ShellPropertyGuids.Device, 15);
			public static readonly DevicePropertyKey DevicePhysicalDeviceObjectName = new(ShellPropertyGuids.Device, 16);
			public static readonly DevicePropertyKey DeviceCapabilities = new(ShellPropertyGuids.Device, 17);
			public static readonly DevicePropertyKey DeviceUiNumber = new(ShellPropertyGuids.Device, 18);
			public static readonly DevicePropertyKey DeviceUpperFilters = new(ShellPropertyGuids.Device, 19);
			public static readonly DevicePropertyKey DeviceLowerFilters = new(ShellPropertyGuids.Device, 20);
			public static readonly DevicePropertyKey DeviceBusTypeGuid = new(ShellPropertyGuids.Device, 21);
			public static readonly DevicePropertyKey DeviceLegacyBusType = new(ShellPropertyGuids.Device, 22);
			public static readonly DevicePropertyKey DeviceBusNumber = new(ShellPropertyGuids.Device, 23);
			public static readonly DevicePropertyKey DeviceEnumeratorName = new(ShellPropertyGuids.Device, 24);
			public static readonly DevicePropertyKey DeviceSecurity = new(ShellPropertyGuids.Device, 25);
			public static readonly DevicePropertyKey DeviceSecurityDescriptorString = new(ShellPropertyGuids.Device, 26);
			public static readonly DevicePropertyKey DeviceDeviceType = new(ShellPropertyGuids.Device, 27);
			public static readonly DevicePropertyKey DeviceExclusive = new(ShellPropertyGuids.Device, 28);
			public static readonly DevicePropertyKey DeviceCharacteristics = new(ShellPropertyGuids.Device, 29);
			public static readonly DevicePropertyKey DeviceAddress = new(ShellPropertyGuids.Device, 30);
			public static readonly DevicePropertyKey DeviceUiNumberPrintfFormat = new(ShellPropertyGuids.Device, 31);
			public static readonly DevicePropertyKey DevicePowerData = new(ShellPropertyGuids.Device, 32);
			public static readonly DevicePropertyKey DeviceRemovalPolicy = new(ShellPropertyGuids.Device, 33);
			public static readonly DevicePropertyKey DeviceRemovalPolicyDefault = new(ShellPropertyGuids.Device, 34);
			public static readonly DevicePropertyKey DeviceRemovalPolicyOverride = new(ShellPropertyGuids.Device, 35);
			public static readonly DevicePropertyKey DeviceInstallState = new(ShellPropertyGuids.Device, 36);
			public static readonly DevicePropertyKey DeviceLocationPaths = new(ShellPropertyGuids.Device, 37);
			public static readonly DevicePropertyKey DeviceBaseContainerId = new(ShellPropertyGuids.Device, 38);

			public static readonly DevicePropertyKey DeviceInterfaceFriendlyName = new(ShellPropertyGuids.DeviceInterface, 2);
			public static readonly DevicePropertyKey DeviceInterfaceClassGuid = new(ShellPropertyGuids.DeviceInterface, 2);

			public static readonly DevicePropertyKey Model = new(ShellPropertyGuids.DeviceContainer, 39);
			public static readonly DevicePropertyKey DeviceInstanceId = new(ShellPropertyGuids.DeviceContainer, 256);
		}

		public enum LocateDeviceNodeFlags
		{
			Normal = 0x00000000,
			Phantom = 0x00000001,
			CancelRemove = 0x00000002,
			NoValidation = 0x00000004,
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
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceProperty([In] string? deviceInterfaceId, in DevicePropertyKey propertyKey, out DevicePropertyType propertyType, byte[]? buffer, ref uint bufferSize, uint flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_DevNode_PropertyW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceNodeProperty(uint deviceInstanceHandle, in DevicePropertyKey propertyKey, out DevicePropertyType propertyType, byte[]? buffer, ref uint bufferSize, uint flags);

		[DllImport("kernel32", ExactSpelling = true, SetLastError = true)]
		public static extern uint DeviceIoControl
		(
			SafeFileHandle deviceHandle,
			uint ioControlCode,
			in byte inputBufferFirstByte,
			uint inputBufferSize,
			ref byte outputBufferFirstByte,
			uint outputBufferSize,
			out uint bytesReturned,
			IntPtr overlapped
		);

		[DllImport("kernel32", EntryPoint = "CreateFileW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern SafeFileHandle CreateFile(string fileName, FileAccessMask desiredAccess, FileShare shareMode, IntPtr securityAttributes, FileMode creationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
	}
}
