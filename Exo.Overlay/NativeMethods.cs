using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Exo.Overlay;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	[DllImport("kernel32", EntryPoint = "GetModuleHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern IntPtr GetModuleHandle(IntPtr zero);

	public unsafe struct WindowClassEx
	{
		public int Size;
		public uint Style;
		public delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr> WindowProcedure;
		public int ClassExtraByteCount;
		public int WindowExtraByteCount;
		public IntPtr InstanceHandle;
		public IntPtr IconHandle;
		public IntPtr CursorHandle;
		public IntPtr BackgroundBrushHandle;
		public IntPtr MenuName;
		public char* ClassName;
		public IntPtr SmallIconHandle;
	}

	[DllImport("user32", EntryPoint = "RegisterClassExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern unsafe ushort RegisterClassEx(WindowClassEx* param);

	[DllImport("user32", EntryPoint = "CreateWindowExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern IntPtr CreateWindowEx(uint exStyle, IntPtr className, string windowName, uint style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menuHandle, IntPtr instanceHandle, IntPtr param);

	[DllImport("user32", EntryPoint = "DestroyWindow", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern uint DestroyWindow(IntPtr windowHandle);

	[DllImport("user32", EntryPoint = "DefWindowProcW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern IntPtr DefWindowProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

	public struct Message
	{
		public IntPtr WindowHandle;
		public uint MessageId;
		public IntPtr WParam;
		public IntPtr LParam;
		public uint Time;
		public Point Point;
		public uint Private;
	}

	[DllImport("user32", EntryPoint = "GetMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern unsafe uint GetMessage(Message* message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

	[DllImport("user32", EntryPoint = "TranslateMessage", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern unsafe uint TranslateMessage(Message* message);

	[DllImport("user32", EntryPoint = "DispatchMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern unsafe nint DispatchMessage(Message* message);

	[DllImport("user32", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern uint PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32", EntryPoint = "RegisterWindowMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern unsafe uint RegisterWindowMessage(ushort* @string);

	[DllImport("user32", EntryPoint = "PostQuitMessage", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	public static extern void PostQuitMessage(int exitCode);

	[DllImport("user32", EntryPoint = "GetWindowLongW", ExactSpelling = true, SetLastError = true)]
	public static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32", EntryPoint = "SetWindowLongW", ExactSpelling = true, SetLastError = true)]
	public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

	[DllImport("user32", EntryPoint = "MoveWindow", ExactSpelling = true, SetLastError = true)]
	public static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

	[DllImport("user32", EntryPoint = "GetCursorPos", ExactSpelling = true, SetLastError = true)]
	private static extern unsafe int GetCursorPos(Point* point);

	[StructLayout(LayoutKind.Sequential)]
	public struct Point
	{
		public int X;
		public int Y;
	}

	[SkipLocalsInit]
	public static unsafe Point GetCursorPos()
	{
		Point point;
		Unsafe.SkipInit(out point);
		if (GetCursorPos(&point) == 0) Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		return point;
	}

	[DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
	public static extern unsafe int Shell_NotifyIcon(NotifyIconMessage message, NotifyIconData* lpData);

	public enum NotifyIconMessage : uint
	{
		Add = 0,
		Modify = 1,
		Delete = 2,
		SetFocus = 3,
		SetVersion = 4,
	}

	public struct NotifyIconData
	{
		public int Size;
		public nint WindowHandle;
		public uint IconId;
		public NotifyIconFeatures Features;
		public uint CallbackMessage;
		public nint IconHandle;
		public FixedString128 TipText;
		public NotifyIconStates State;
		public NotifyIconStates StateMask;
		public FixedString256 InfoText;
		public uint Version;
		public FixedString64 InfoTitleText;
		public NotifyIconInfoFlags InfoFlags;
		public Guid ItemGuid;
		public nint BalloonIconHandle;
	}

	[Flags]
	public enum NotifyIconFeatures : uint
	{
		Message = 0x00000001,
		Icon = 0x00000002,
		Tip = 0x00000004,
		State = 0x00000008,
		Info = 0x00000010,
		Guid = 0x00000020,
		Realtime = 0x00000040,
		ShowTip = 0x00000080,
	}

	[Flags]
	public enum NotifyIconStates : uint
	{
		Hidden = 0x00000001,
		SharedIcon = 0x00000002,
	}

	[Flags]
	public enum NotifyIconInfoFlags : uint
	{
		None = 0x00000000,
		IconMask = 0x0000000F,
		NoSound = 0x00000010,
		LargeIcon = 0x00000020,
		RespectQuietTime = 0x00000080,
	}

	public enum NotifyIconInfoIcon : byte
	{
		None = 0x00000000,
		Info = 0x00000001,
		Warning = 0x00000002,
		Error = 0x00000003,
		User = 0x00000004,
	}

#pragma warning disable IDE0044 // Add readonly modifier
	[InlineArray(64)]
	public struct FixedString64
	{
		private ushort _element0;
	}

	[InlineArray(128)]
	public struct FixedString128
	{
		private ushort _element0;
	}

	[InlineArray(256)]
	public struct FixedString256
	{
		private ushort _element0;
	}
#pragma warning restore IDE0044 // Add readonly modifier

	[DllImport("user32", EntryPoint = "LoadImageW", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern nint LoadImage(nint instanceHandle, nint resourceId, uint type, int cx, int cy, uint flags);

	[DllImport("comctl32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = false)]
	private static extern unsafe int LoadIconMetric(nint instanceHandle, nint resourceId, IconMetric metric, nint* iconHandle);

	public enum IconMetric
	{
		Small,
		Large
	}

	public static unsafe nint LoadIconMetric(nint instanceHandle, nint resourceId, IconMetric metric)
	{
		nint iconHandle;
		int hr = LoadIconMetric(instanceHandle, resourceId, metric, &iconHandle);
		if (hr != 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}
		return iconHandle;
	}

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int DestroyIcon(nint iconHandle);
}
