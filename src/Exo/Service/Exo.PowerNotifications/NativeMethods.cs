using System.Runtime.InteropServices;

namespace Exo.PowerNotifications;

internal static class NativeMethods
{
	[Flags]
	public enum DeviceNotificationFlags
	{
		WindowHandle = 0x00000000,
		ServiceHandle = 0x00000001,
		Callback = 0x00000002,
	}

	// From what I understand from the documentation, this should only be needed for interactive session apps.
	[DllImport("User32", ExactSpelling = true, SetLastError = true)]
	public static extern IntPtr RegisterSuspendResumeNotification(IntPtr hRecipient, DeviceNotificationFlags flags);

	[DllImport("User32", ExactSpelling = true, SetLastError = true)]
	public static extern uint UnregisterSuspendResumeNotification(IntPtr handle);

	[DllImport("User32", ExactSpelling = true, SetLastError = true)]
	public static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, Guid powerSettingGuid, DeviceNotificationFlags flags);

	[DllImport("User32", ExactSpelling = true, SetLastError = true)]
	public static extern uint UnregisterPowerSettingNotification(IntPtr handle);
}

internal sealed class SafeSuspendResumeNotificationHandle : SafeHandle
{
	internal SafeSuspendResumeNotificationHandle() : base(IntPtr.Zero, true) { }

	internal SafeSuspendResumeNotificationHandle(IntPtr handle) : this()
		=> SetHandle(handle);

	protected override bool ReleaseHandle()
		=> NativeMethods.UnregisterSuspendResumeNotification(handle) != 0;

	public override bool IsInvalid => handle == IntPtr.Zero;
}


internal sealed class SafePowerSettingNotificationHandle : SafeHandle
{
	internal SafePowerSettingNotificationHandle() : base(IntPtr.Zero, true) { }

	internal SafePowerSettingNotificationHandle(IntPtr handle) : this()
		=> SetHandle(handle);

	protected override bool ReleaseHandle()
		=> NativeMethods.UnregisterPowerSettingNotification(handle) != 0;

	public override bool IsInvalid => handle == IntPtr.Zero;
}
