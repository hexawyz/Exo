using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace DeviceTools.DisplayDevices
{
	public sealed class SafePhysicalMonitorHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafePhysicalMonitorHandle(IntPtr handle)
			: base(true)
		{
			SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.DestroyPhysicalMonitor(handle) != 0;
		}
	}
}
