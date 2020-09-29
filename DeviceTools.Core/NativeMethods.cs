using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools
{
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
			uint MemberIndex,
			ref DeviceInterfaceData DeviceInterfaceData
		);

		[DllImport("SetupAPI", EntryPoint = "SetupDiEnumDeviceInterfaces", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern int SetupDiEnumDeviceInterfaces
		(
			SafeDeviceInfoListHandle deviceInfoSet,
			IntPtr zero,
			in Guid interfaceClassGuid,
			uint MemberIndex,
			ref DeviceInterfaceData DeviceInterfaceData
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
				size = IntPtr.Size == 4 ? 6 : 8; // sizeof(SP_DeviceInterfaceDETAIL_DATA) in C accounts for the padding between cbSize and DevicePath.
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
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceList(in Guid interfaceClassGuid, [In] string? deviceID, ref char buffer, uint bufferLength, GetDeviceInterfaceListFlags flags);

		[DllImport("CfgMgr32", EntryPoint = "CM_Get_Device_Interface_List_SizeW", ExactSpelling = true, CharSet = CharSet.Unicode)]
		public static extern ConfigurationManagerResult ConfigurationManagerGetDeviceInterfaceListSize(out uint length, in Guid interfaceClassGuid, [In] string? deviceID, GetDeviceInterfaceListFlags flags);

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
