using System;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	partial class NativeMethods
	{
		[DllImport("kernel32", EntryPoint = "LoadLibraryW", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr LoadLibrary(string lpLibFileName);

		[DllImport("kernel32", EntryPoint = "GetProcAddress", ExactSpelling = true, PreserveSig = true, CharSet = CharSet.Ansi, SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
	}
}
