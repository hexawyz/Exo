using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace DeviceTools;

public static class SafeFileHandleExtensions
{
	public static unsafe uint IoControl(this SafeFileHandle deviceFileHandle, uint ioControlCode, ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
		=> NativeMethods.DeviceIoControl
		(
			deviceFileHandle,
			ioControlCode,
			inputBuffer.GetPinnableReference(),
			(uint)inputBuffer.Length,
			ref outputBuffer.GetPinnableReference(),
			(uint)outputBuffer.Length,
			out uint bytesReturned,
			null
		) == 0 ?
			throw new Win32Exception(Marshal.GetLastWin32Error()) :
			bytesReturned;

	public static unsafe bool IoControl(this SafeFileHandle deviceFileHandle, uint ioControlCode, ReadOnlyMemory<byte> inputBuffer, Memory<byte> outputBuffer, NativeOverlapped* overlapped)
	{
		using (var imh = inputBuffer.Pin())
		using (var omh = outputBuffer.Pin())
		{
			uint result = NativeMethods.DeviceIoControl
			(
				deviceFileHandle,
				ioControlCode,
				(byte*)imh.Pointer,
				(uint)inputBuffer.Length,
				(byte*)omh.Pointer,
				(uint)outputBuffer.Length,
				null,
				overlapped
			);

			if (result == 0)
			{
				int errorCode = Marshal.GetLastWin32Error();
				if (errorCode != NativeMethods.ErrorIoPending)
				{
					throw new Win32Exception(errorCode);
				}
				return false;
			}
			return true;
		}
	}

#if NET8_0_OR_GREATER
	[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_ThreadPoolBinding")]
	private static extern ThreadPoolBoundHandle GetThreadPoolBinding(SafeFileHandle handle);

	[UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Open")]
	private static extern SafeFileHandle Open(SafeFileHandle? @this, string fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, long preallocationSize, UnixFileMode? unixCreateMode = null);

	internal static SafeFileHandle Open(string fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, long preallocationSize, UnixFileMode? unixCreateMode = null)
		=> Open(null, fullPath, mode, access, share, options, preallocationSize, unixCreateMode);

	//public static unsafe void BeginIoControl(this SafeFileHandle deviceFileHandle, uint ioControlCode, ReadOnlyMemory<byte> inputBuffer, Memory<byte> outputBuffer, PreAllocatedOverlapped preAllocatedOverlapped)
	//{
	//}

	//public static unsafe ValueTask<int> IoControlAsync(this SafeFileHandle deviceFileHandle, uint ioControlCode, ReadOnlyMemory<byte> inputBuffer, Memory<byte> outputBuffer)
	//{
	//	static void OnIoCompleted(uint errorCode, uint numBytes, NativeOverlapped* pOVERLAP)
	//	{
	//		GetThreadPoolBinding(deviceFileHandle).
	//	}

	//	var overlapped = GetThreadPoolBinding(deviceFileHandle).AllocateNativeOverlapped(OnIoCompleted, 0, null);
	//	try
	//	{
	//		using (var imh = inputBuffer.Pin())
	//		using (var omh = outputBuffer.Pin())
	//		{
	//			uint result = NativeMethods.DeviceIoControl
	//			(
	//				deviceFileHandle,
	//				ioControlCode,
	//				(byte*)imh.Pointer,
	//				(uint)inputBuffer.Length,
	//				(byte*)omh.Pointer,
	//				(uint)outputBuffer.Length,
	//				null,
	//				overlapped
	//			);

	//			if (result == 0)
	//			{
	//				int errorCode = Marshal.GetLastWin32Error();
	//				if (errorCode != NativeMethods.ErrorIoPending)
	//				{
	//					throw new Win32Exception(errorCode);
	//				}
	//				return false;
	//			}
	//			return true;
	//		}
	//	}
	//	finally
	//	{
	//		GetThreadPoolBinding(deviceFileHandle).FreeNativeOverlapped(overlapped);
	//	}
	//}
#endif
}
