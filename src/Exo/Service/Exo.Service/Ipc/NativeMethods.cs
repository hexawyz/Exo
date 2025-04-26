using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace Exo.Service.Ipc;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	[DllImport("kernel32", ExactSpelling = true, PreserveSig = true, SetLastError = false)]
	private static extern unsafe uint GetNamedPipeClientProcessId(nint pipeHandle, uint* clientProcessId);

	public static unsafe int GetNamedPipeClientProcessId(SafePipeHandle safePipeHandle)
	{
		uint processId;
		bool acquired = false;
		try
		{
			safePipeHandle.DangerousAddRef(ref acquired);
			if (GetNamedPipeClientProcessId(safePipeHandle.DangerousGetHandle(), &processId) == 0)
			{
				uint error = (uint)Marshal.GetLastSystemError();
				if ((error & 0x80000000U) != 0) error = error & 0x0000FFFFU | 0x80070000U;
				Marshal.ThrowExceptionForHR((int)error);
			}
			return (int)processId;
		}
		finally
		{
			if (acquired) safePipeHandle.DangerousRelease();
		}
	}
}
