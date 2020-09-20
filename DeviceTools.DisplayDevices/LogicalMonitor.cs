using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools.DisplayDevices
{
	public sealed class LogicalMonitor
	{
		[UnmanagedCallersOnly]
		private static uint EnumDisplayMonitorsCallback(IntPtr monitorHandle, /* in NativeMethods.Rectangle */ IntPtr deviceContextHandle, IntPtr clipRectangle, IntPtr data)
		{
			if (GCHandle.FromIntPtr(data).Target is List<LogicalMonitor> list)
			{
				list.Add(new LogicalMonitor(monitorHandle));
			}

			return 1;
		}

		public static unsafe LogicalMonitor[] GetAll()
		{
			var list = new List<LogicalMonitor>();
			var handle = GCHandle.Alloc(list);
			try
			{
				if
				(
					NativeMethods.EnumDisplayMonitors
					(
						IntPtr.Zero,
						MemoryMarshal.GetReference(default(Span<NativeMethods.Rectangle>)),
						(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, uint>)(delegate*<IntPtr, IntPtr, IntPtr, IntPtr, uint>)&EnumDisplayMonitorsCallback,
						GCHandle.ToIntPtr(handle)
					) == 0
				)
				{
					throw new Win32Exception(Marshal.GetLastWin32Error());
				}
			}
			finally
			{
				handle.Free();
			}
			return list.ToArray();
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public IntPtr Handle { get; }

		public bool IsPrimary { get; }

		public string Name { get; }

		private LogicalMonitor(IntPtr handle)
		{
			this.Handle = handle;
			var info = new NativeMethods.LogicalMonitorInfoEx { Size = Unsafe.SizeOf<NativeMethods.LogicalMonitorInfoEx>() };
			// TODO: Avoid throwing exceptions in the constructor, which is (indirectly) called from unmanaged code.
			if (NativeMethods.GetMonitorInfo(handle, ref info) == 0)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}
			IsPrimary = (info.Flags & NativeMethods.MonitorInfoFlags.Primary) != 0;
			Name = info.DeviceName.ToString();
		}
	}
}
