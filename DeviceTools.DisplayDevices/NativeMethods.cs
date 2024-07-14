using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using DeviceTools.DisplayDevices.Configuration;

namespace DeviceTools.DisplayDevices;

// FIXME: Endianness of some data structures with bit fields on Big Endian hosts ?
[SuppressUnmanagedCodeSecurity]
internal static partial class NativeMethods
{
	public const int ErrorGraphicsDdcCiInvalidMessageCommand = unchecked((int)0xC0262589);
	public const int ErrorGraphicsDdcCiInvalidMessageLength = unchecked((int)0xC026258A);
	public const int ErrorGraphicsDdcCiInvalidMessageChecksum = unchecked((int)0xC026258B);
	public const int ErrorInsufficientBuffer = 122;
	public const int ErrorInvalidParameter = 0x57;

	// HRESULT version of ErrorInvalidParameter
	public const uint ErrorInvalidArgument = 0x80070057;

	public const uint DisplayConfigPathModeIndexInvalid = 0xffffffff;
	public const ushort DisplayConfigPathTargetModeIndexInvalid = 0xffff;
	public const ushort DisplayConfigPathDesktopImageIndexInvalid = 0xffff;
	public const ushort DisplayConfigPathSourceModeIndexInvalid = 0xffff;
	public const ushort DisplayConfigPathCloneGroupInvalid = 0xffff;

	[StructLayout(LayoutKind.Sequential)]
	public struct Point
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rectangle
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	// Just remove the DeviceName part to make it a "non-Ex" info.
	public struct LogicalMonitorInfoEx
	{
		public int Size;
		public Rectangle MonitorArea;
		public Rectangle WorkArea;
		public MonitorInfoFlags Flags;
		public FixedString32 DeviceName;
	}

	[Flags]
	public enum MonitorInfoFlags
	{
		None = 0,
		Primary = 1,
	}

	public enum MonitorFromPointFlags
	{
		DefaultToNull = 0,
		DefaultToPrimary = 1,
		DefaultToNearest = 2,
	}

	public enum MonitorDpiType
	{
		EffectiveDpi = 0,
		AngularDpi = 1,
		RawDpi = 2,
	}

	public struct DisplayDevice
	{
		public int Size;
		public FixedString32 DeviceName;
		public FixedString128 DeviceString;
		public DisplayDeviceFlags StateFlags;
		public FixedString128 DeviceId;
		public FixedString128 DeviceKey;
	}

	public enum EnumDisplayDeviceFlags
	{
		None = 0x00000000,
		GetDeviceInterfaceName = 0x00000001,
	}

	public readonly struct PhysicalMonitor
	{
#pragma warning disable CS0649
		public readonly IntPtr Handle;
		public readonly FixedString128 Description;
#pragma warning restore CS0649
	}

	public enum VcpCodeType
	{
		Momentary,
		SetParameter,
	}

	[Flags]
	public enum QueryDeviceConfigFlags
	{
		AllPaths = 0x00000001,
		OnlyActivePaths = 0x00000002,
		DatabaseCurrent = 0x00000004,
		VirtualModeAware = 0x00000010,
		IncludeHmd = 0x00000020,
	}

	[Flags]
	public enum DisplayConfigPathInfoFlags
	{
		PathActive = 0x00000001,
		//PreferredUnscaled = 0x00000004,
		SupportVirtualMode = 0x00000008,
	}

	[Flags]
	public enum DisplayConfigPathSourceInfoStatus
	{
		InUse = 0x00000001,
	}

	[Flags]
	public enum DisplayConfigPathTargetInfoStatus
	{
		InUse = 0x00000001,
		Forcible = 0x00000002,
		ForcedAvailabilityBoot = 0x00000004,
		ForcedAvailabilityPath = 0x00000008,
		ForcedAvailabilitySystem = 0x00000010,
		IsHmd = 0x00000020,
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct SourceModeInfo
	{
		[FieldOffset(0)]
		public uint ModeInfoIdx;
		[FieldOffset(0)]
		public ushort CloneGroupId;
		[FieldOffset(2)]
		public ushort SourceModeInfoIdx;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct TargetModeInfo
	{
		[FieldOffset(0)]
		public uint ModeInfoIdx;
		[FieldOffset(0)]
		public ushort DesktopModeInfoIdx;
		[FieldOffset(2)]
		public ushort TargetModeInfoIdx;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigPathSourceInfo
	{
		public Luid AdapterId;
		public uint Id;
		public SourceModeInfo ModeInfo;
		public DisplayConfigPathSourceInfoStatus StatusFlags;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigPathTargetInfo
	{
		public Luid AdapterId;
		public uint Id;
		public TargetModeInfo ModeInfo;
		public VideoOutputTechnology OutputTechnology;
		public Rotation Rotation;
		public Scaling Scaling;
		public Rational RefreshRate;
		public ScanlineOrdering ScanLineOrdering;
		private uint _targetAvailable;
		public bool TargetAvailable
		{
			get => _targetAvailable != 0;
			set => _targetAvailable = value ? 1U : 0;
		}
		public DisplayConfigPathTargetInfoStatus StatusFlags;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigPathInfo
	{
		public DisplayConfigPathSourceInfo SourceInfo;
		public DisplayConfigPathTargetInfo TargetInfo;
		public DisplayConfigPathInfoFlags Flags;
	}

	public enum DisplayConfigDeviceInfoType
	{
		GetSourceName = 1,
		GetTargetName = 2,
		GetTargetPreferredMode = 3,
		GetAdapterName = 4,
		SetTargetPersistence = 5,
		GetTargetBaseType = 6,
		GetSupportVirtualResolution = 7,
		SetSupportVirtualResolution = 8,
		GetAdvancedColorInfo = 9,
		SetAdvancedColorState = 10,
		GetSdrWhiteLevel = 11,
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Luid : IEquatable<Luid>
	{
		public Luid(long value)
			=> (LowPart, HighPart) = ((uint)value, (int)(value >> 32));

		public readonly uint LowPart;
		public readonly int HighPart;

		public long ToInt64() => (long)HighPart << 32 | LowPart;

		public override bool Equals(object? obj) => obj is Luid luid && Equals(luid);
		public bool Equals(Luid other) => LowPart == other.LowPart && HighPart == other.HighPart;
		public override int GetHashCode() => HashCode.Combine(LowPart, HighPart);

		public static bool operator ==(Luid left, Luid right) => left.Equals(right);
		public static bool operator !=(Luid left, Luid right) => !(left == right);
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigDeviceInfoHeader
	{
		public DisplayConfigDeviceInfoType Type;
		public int Size;
		public Luid AdapterId;
		public uint Id;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigAdapterName
	{
		public DisplayConfigDeviceInfoHeader Header;
		public FixedString128 AdapterDevicePath;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigSourceDeviceName
	{
		public DisplayConfigDeviceInfoHeader Header;
		public FixedString32 ViewGdiDeviceName;
	}

	[Flags]
	public enum DisplayConfigTargetDeviceNameFlags
	{
		FriendlyNameFromEdid = 0x00000001,
		FriendlyNameForced = 0x00000002,
		EdidIdsValid = 0x00000004,
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfig2DRegion
	{
		public uint Cx;
		public uint Cy;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigTargetDeviceName
	{
		public DisplayConfigDeviceInfoHeader Header;
		public DisplayConfigTargetDeviceNameFlags Flags;
		public VideoOutputTechnology OutputTechnology;
		public ushort EdidManufactureId;
		public ushort EdidProductCodeId;
		public uint ConnectorInstance;
		public FixedString64 MonitorFriendlyDeviceName;
		public FixedString128 MonitorDevicePath;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigVideoSignalInfo
	{
		public ulong PixelRate;
		public Rational HorizontalSyncFreq;
		public Rational VerticalSyncFreq;
		public DisplayConfig2DRegion ActiveSize;
		public DisplayConfig2DRegion TotalSize;

		private ushort _videoStandard;
		private ushort _additionalSignalInfo;

		public VideoSignalStandard VideoStandard
		{
			get => (VideoSignalStandard)_videoStandard;
			set => _videoStandard = (byte)value;
		}

		public int VerticalSyncFreqDivider
		{
			get => _additionalSignalInfo & 0b0011_1111;
			set => _additionalSignalInfo = (ushort)(_additionalSignalInfo & ~0b0011_1111 | value & 0b0011_1111);
		}

		public ScanlineOrdering ScanLineOrdering;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigTargetMode
	{
		public DisplayConfigVideoSignalInfo TargetVideoSignalInfo;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigSourceMode
	{
		public uint Width;
		public uint Height;
		public PixelFormat PixelFormat;
		public Point Position;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DisplayConfigDesktopImageInfo
	{
		public Point PathSourceSize;
		public Rectangle DesktopImageRegion;
		public Rectangle DesktopImageClip;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct DisplayConfigModeInfo
	{
		[FieldOffset(0)]
		public ModeInfoType InfoType;
		[FieldOffset(4)]
		public uint Id;
		[FieldOffset(8)]
		public Luid AdapterId;
		[FieldOffset(16)]
		public DisplayConfigTargetMode TargetMode;
		[FieldOffset(16)]
		public DisplayConfigSourceMode SourceMode;
		[FieldOffset(16)]
		public DisplayConfigDesktopImageInfo DesktopImageInfo;
	}

	private static ReadOnlySpan<char> TruncateToFirstNull(ReadOnlySpan<char> characters)
		=> characters.IndexOf('\0') is int i and >= 0 ? characters.Slice(0, i) : characters;

	private static string StructToString<T>(in T value)
		where T : struct
		=> TruncateToFirstNull(MemoryMarshal.Cast<T, char>(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(value), 1))).ToString();

	// A fixed length string buffer of 32 characters.
	[StructLayout(LayoutKind.Explicit, Size = 32 * sizeof(char))]
	public readonly struct FixedString32
	{
		public override string ToString() => StructToString(this);
	}

	// A fixed length string buffer of 64 characters.
	[StructLayout(LayoutKind.Explicit, Size = 64 * sizeof(char))]
	public readonly struct FixedString64
	{
		public override string ToString() => StructToString(this);
	}

	// A fixed length string buffer of 128 characters.
	[StructLayout(LayoutKind.Explicit, Size = 128 * sizeof(char))]
	public readonly struct FixedString128
	{
		public override string ToString() => StructToString(this);
	}

	[DllImport("User32", EntryPoint = "EnumDisplayDevicesW", ExactSpelling = true, CharSet = CharSet.Unicode)]
	public static extern unsafe uint EnumDisplayDevices
	(
		[In] string? device,
		uint deviceIndex,
		ref DisplayDevice displayDevice,
		EnumDisplayDeviceFlags dwFlags
	);

	[DllImport("User32", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint EnumDisplayMonitors(IntPtr deviceContext, in Rectangle clipRectangle, delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, uint> callback, IntPtr data);

	[DllImport("User32", EntryPoint = "GetMonitorInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern unsafe uint GetMonitorInfo(IntPtr monitor, ref LogicalMonitorInfoEx monitorInfo);

	[DllImport("User32", EntryPoint = "MonitorFromPoint", ExactSpelling = true, SetLastError = true)]
	public static extern IntPtr MonitorFromPoint(Point point, MonitorFromPointFlags flags);

	[DllImport("Shcore", EntryPoint = "GetDpiForMonitor", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint GetDpiForMonitor(IntPtr monitor, MonitorDpiType dpiType, uint* dpiX, uint* dpiY);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern uint GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitorHandle, out uint numberOfPhysicalMonitors);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern uint GetPhysicalMonitorsFromHMONITOR(IntPtr monitorHandle, uint physicalMonitorArraySize, [Out] PhysicalMonitor[] physicalMonitors);

	//[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	//public static extern uint DestroyPhysicalMonitors(uint physicalMonitorArraySize, PhysicalMonitorDescription[] physicalMonitors);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern uint DestroyPhysicalMonitor(IntPtr physicalMonitorHandle);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern uint GetCapabilitiesStringLength(SafePhysicalMonitorHandle physicalMonitorHandle, out uint capabilitiesStringLength);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint CapabilitiesRequestAndCapabilitiesReply(SafePhysicalMonitorHandle physicalMonitorHandle, byte* asciiCapabilitiesStringFirstCharacter, uint capabilitiesStringLength);

	[DllImport("Dxva2", EntryPoint = "GetVCPFeatureAndVCPFeatureReply", ExactSpelling = true, SetLastError = true)]
	public static extern uint GetVcpFeatureAndVcpFeatureReply(SafePhysicalMonitorHandle physicalMonitorHandle, byte vcpCode, out VcpCodeType vcpCodeType, out uint currentValue, out uint maximumValue);

	[DllImport("Dxva2", EntryPoint = "SetVCPFeature", ExactSpelling = true, SetLastError = true)]
	public static extern uint SetVcpFeature(SafePhysicalMonitorHandle physicalMonitorHandle, byte vcpCode, uint newValue);

	[DllImport("Dxva2", ExactSpelling = true, SetLastError = true)]
	public static extern uint SaveCurrentSettings(SafePhysicalMonitorHandle physicalMonitorHandle);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int GetDisplayConfigBufferSizes(QueryDeviceConfigFlags flags, out int numPathArrayElements, out int numModeInfoArrayElements);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int QueryDisplayConfig
	(
		QueryDeviceConfigFlags flags,
		ref int numPathArrayElements,
		ref DisplayConfigPathInfo pathArrayFirstElement,
		ref int numModeInfoArrayElements,
		ref DisplayConfigModeInfo modeInfoArrayFirstElement,
		out Topology currentTopologyId
	);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int QueryDisplayConfig
	(
		QueryDeviceConfigFlags flags,
		ref int numPathArrayElements,
		ref DisplayConfigPathInfo pathArrayFirstElement,
		ref int numModeInfoArrayElements,
		ref DisplayConfigModeInfo modeInfoArrayFirstElement,
		IntPtr zero
	);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigSourceDeviceName requestPacket);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

	[DllImport("User32", ExactSpelling = true, SetLastError = false)]
	public static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigAdapterName requestPacket);
}
