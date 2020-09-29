using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeviceTools
{
	public sealed class SafeDeviceInfoListHandle : SafeHandleMinusOneIsInvalid
	{
		private SafeDeviceInfoListHandle()
			: base(true) { }

		public SafeDeviceInfoListHandle(IntPtr handle)
			: base(true) => SetHandle(handle);

		protected override bool ReleaseHandle() => NativeMethods.SetupDiDestroyDeviceInfoList(handle) != 0;
	}
}
