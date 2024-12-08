#if !NETCOREAPP3_0_OR_GREATER
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeviceTools;

internal partial class NativeMethods
{
	[DllImport("kernel32", EntryPoint = "LoadLibraryW", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr LoadLibrary(string lpLibFileName);

	[DllImport("kernel32", EntryPoint = "GetProcAddress", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Ansi, SetLastError = true)]
	public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

	[DllImport("kernel32", EntryPoint = "FreeLibrary", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool FreeLibrary(IntPtr hLibModule);
}

public static class NativeLibrary
{
	public static IntPtr Load(string libraryPath)
	{
		if (libraryPath is null) throw new ArgumentNullException(nameof(libraryPath));

		var address = NativeMethods.LoadLibrary(libraryPath);

		if (address == (nint)0)
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		return address;
	}

	public static void Free(IntPtr handle)
	{
		if (!NativeMethods.FreeLibrary(handle))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
	}

	public static bool TryGetExport(IntPtr handle, string name, out IntPtr address)
	{
		if (handle == (nint)0) throw new ArgumentNullException(nameof(handle));
		if (name is null) throw new ArgumentNullException(nameof(name));

		address = NativeMethods.GetProcAddress(handle, name);

		return address != IntPtr.Zero;
	}
}
#endif
