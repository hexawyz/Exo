using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace Exo.Service.Ipc;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	[DllImport("kernel32", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
	private static extern unsafe uint GetNamedPipeClientProcessId(nint pipeHandle, uint* clientProcessId);

	public static unsafe int GetNamedPipeClientProcessId(SafePipeHandle safePipeHandle)
	{
		uint processId;
		bool acquired = false;
		try
		{
			safePipeHandle.DangerousAddRef(ref acquired);
			if (GetNamedPipeClientProcessId(safePipeHandle.DangerousGetHandle(), &processId) == 0) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			return (int)processId;
		}
		finally
		{
			if (acquired) safePipeHandle.DangerousRelease();
		}
	}
}
