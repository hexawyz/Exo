using Microsoft.Win32.SafeHandles;

namespace DeviceTools;

internal sealed class SafeDeviceQueryHandle : SafeHandleMinusOneIsInvalid
{
	private SafeDeviceQueryHandle()
		: base(true) { }

	public SafeDeviceQueryHandle(IntPtr handle)
		: base(true) => SetHandle(handle);

	protected override bool ReleaseHandle()
	{
		NativeMethods.DeviceCloseObjectQuery(handle);
		return true;
	}
}
