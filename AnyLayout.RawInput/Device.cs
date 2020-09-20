using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AnyLayout.RawInput
{
	public static class Device
	{
		/// <summary>Opens a device file.</summary>
		/// <remarks>
		/// <para>
		/// Use of this function is required to open device because <see cref="FileStream"/> won't agree to randomly opening device files.
		/// This is somewhat understandable, though, as <see cref="FileStream"/> was conceived as a stream and may lack sufficient features to operate on devices.
		/// </para>
		/// <para>
		/// Driver device file names will usually be of the form <c>\\.\DeviceName</c> or <c>\\?\DosDeviceName</c>. This is a name (symlink) defined by the driver.
		/// </para>
		/// </remarks>
		/// <param name="deviceName">The name of the devide file.</param>
		/// <param name="access">The required access</param>
		/// <returns>A safe file handle, that can be used to issue IO control, or to create a <see cref="FileStream"/> instance if required.</returns>
		public static SafeFileHandle OpenHandle(string deviceName, DeviceAccess access)
		{
			var handle = NativeMethods.CreateFile
			(
				deviceName,
				access switch
				{
					DeviceAccess.None => 0,
					DeviceAccess.Read => NativeMethods.FileAccessMask.GenericRead,
					DeviceAccess.Write=> NativeMethods.FileAccessMask.GenericWrite,
					DeviceAccess.ReadWrite => NativeMethods.FileAccessMask.GenericRead | NativeMethods.FileAccessMask.GenericWrite,
					_ => throw new ArgumentOutOfRangeException(nameof(access))
				},
				FileShare.ReadWrite,
				IntPtr.Zero,
				FileMode.Open,
				0,
				IntPtr.Zero
			);

			if (handle.IsInvalid)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return handle;
		}
	}
}
