using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DeviceTools
{
	public static class SafeFileHandleExtensions
	{
		public static Span<byte> IoControl(this SafeFileHandle deviceFileHandle, uint ioControlCode, ReadOnlySpan<byte> input, Span<byte> outputBuffer)
			=> NativeMethods.DeviceIoControl
			(
				deviceFileHandle,
				ioControlCode,
				input.GetPinnableReference(),
				(uint)input.Length,
				ref outputBuffer.GetPinnableReference(),
				(uint)outputBuffer.Length,
				out uint bytesReturned,
				IntPtr.Zero
			) == 0 ?
				throw new Win32Exception(Marshal.GetLastWin32Error()) :
				outputBuffer.Slice(0, checked((int)bytesReturned));
	}
}
