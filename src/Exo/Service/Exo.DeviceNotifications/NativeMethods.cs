using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Exo.DeviceNotifications
{
	class NativeMethods
	{
		public enum BroadcastDeviceType
		{
			DeviceInterface = 0x00000005,
			Handle = 0x00000006,
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct DeviceBrodcastDeviceInterface
		{
			public int Size;
			public BroadcastDeviceType DeviceType;
			private readonly int _reserved;
			public Guid ClassGuid;
			//private char Name; // First character of a null-terminated string
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceBroadcastHandle
		{
			public int Size;
			public BroadcastDeviceType DeviceType;
			private readonly int _reserved;
			public IntPtr DeviceHandle;
			public IntPtr DeviceNotifyHandle;
			public Guid EventGuid;
			public int NameOffset;
			//private byte dbch_data; // First byte of a variable length buffer.
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceBroadcastHeader
		{
			public int Size;
			public BroadcastDeviceType DeviceType;
			private readonly int _reserved;
		}

		[Flags]
		public enum DeviceNotificationFlags
		{
			WindowHandle = 0x00000000,
			ServiceHandle = 0x00000001,
			AllInterfaceClasses = 0x00000004,
		}

		public const int BROADCAST_QUERY_DENY = 0x424D5144;

		public enum DeviceBroadcastType
		{
			DeviceArrival = 0x8000,
			DeviceQueryRemove = 0x8001,
			DeviceQueryRemoveFailed = 0x8002,
			DeviceRemovePending = 0x8003,
			DeviceRemoveComplete = 0x8004,
			CustomEvent = 0x8006,
		}

		[DllImport("User32", ExactSpelling = true, SetLastError = true)]
		public static extern IntPtr RegisterDeviceNotificationW(IntPtr hRecipient, DeviceBrodcastDeviceInterface notificationFilter, DeviceNotificationFlags flags);

		[DllImport("User32", ExactSpelling = true, SetLastError = true)]
		public static extern IntPtr RegisterDeviceNotificationW(IntPtr hRecipient, DeviceBroadcastHandle notificationFilter, DeviceNotificationFlags flags);

		[DllImport("User32", ExactSpelling = true)]
		public static extern bool UnregisterDeviceNotification(IntPtr handle);
	}

	internal sealed class SafeDeviceNotificationHandle : SafeHandle
	{
		internal SafeDeviceNotificationHandle() : base(IntPtr.Zero, true) { }

		internal SafeDeviceNotificationHandle(IntPtr handle) : this()
			=> SetHandle(handle);

		protected override bool ReleaseHandle()
		{
			NativeMethods.UnregisterDeviceNotification(handle);
			return true;
		}

		public override bool IsInvalid => handle == IntPtr.Zero;
	}
}
