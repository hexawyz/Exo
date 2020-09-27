using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace DeviceTools.DisplayDevices
{
	[SuppressUnmanagedCodeSecurity]
	internal static class NativeMethods
	{
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
		public static extern uint CapabilitiesRequestAndCapabilitiesReply(SafePhysicalMonitorHandle physicalMonitorHandle, ref byte asciiCapabilitiesStringFirstCharacter, uint capabilitiesStringLength);
	}
}
