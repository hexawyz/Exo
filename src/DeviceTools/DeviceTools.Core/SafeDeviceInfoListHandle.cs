using Microsoft.Win32.SafeHandles;

namespace DeviceTools;

public sealed class SafeDeviceInfoListHandle : SafeHandleMinusOneIsInvalid
{
	private SafeDeviceInfoListHandle()
		: base(true) { }

	public SafeDeviceInfoListHandle(IntPtr handle)
		: base(true) => SetHandle(handle);

	protected override bool ReleaseHandle() => NativeMethods.SetupDiDestroyDeviceInfoList(handle) != 0;
}
