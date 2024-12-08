using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace Exo.Overlay;

#pragma warning disable CS0649
[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	[DllImport("kernel32", EntryPoint = "GetModuleHandleW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr GetModuleHandle(nint zero);

	public unsafe struct WindowClassEx
	{
		public int Size;
		public uint Style;
		public delegate* unmanaged<nint, uint, nint, nint, nint> WindowProcedure;
		public int ClassExtraByteCount;
		public int WindowExtraByteCount;
		public nint InstanceHandle;
		public nint IconHandle;
		public nint CursorHandle;
		public nint BackgroundBrushHandle;
		public nint MenuName;
		public ushort* ClassName;
		public nint SmallIconHandle;
	}

	[DllImport("user32", EntryPoint = "RegisterClassExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern unsafe ushort RegisterClassEx(WindowClassEx* param);

	[DllImport("user32", EntryPoint = "CreateWindowExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateWindowEx(uint exStyle, nint className, string windowName, uint style, int x, int y, int width, int height, nint parentWindowHandle, nint menuHandle, nint instanceHandle, nint param);

	public unsafe struct WindowCreationParameters
	{
		public nint CreateParameters;
		public nint InstanceHandle;
		public nint MenuHandle;
		public nint ParentWindowHandle;
		public int Height;
		public int Width;
		public int Y;
		public int X;
		public uint Style;
		public ushort* Name;
		public nint ClassName;
		public uint ExStyle;
	}

	[DllImport("user32", EntryPoint = "DestroyWindow", ExactSpelling = true, SetLastError = true)]
	public static extern uint DestroyWindow(nint windowHandle);

	[DllImport("user32", EntryPoint = "SetForegroundWindow", ExactSpelling = true, SetLastError = true)]
	public static extern uint SetForegroundWindow(nint windowHandle);

	[DllImport("user32", EntryPoint = "DefWindowProcW", ExactSpelling = true, SetLastError = true)]
	public static extern nint DefWindowProc(nint windowHandle, uint message, nint wParam, nint lParam);

	public struct Message
	{
		public nint WindowHandle;
		public uint MessageId;
		public nint WParam;
		public nint LParam;
		public uint Time;
		public Point Point;
		public uint Private;
	}

	[DllImport("user32", EntryPoint = "GetMessageW", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint GetMessage(Message* message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

	[DllImport("user32", EntryPoint = "TranslateMessage", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe uint TranslateMessage(Message* message);

	[DllImport("user32", EntryPoint = "DispatchMessageW", ExactSpelling = true, SetLastError = true)]
	public static extern unsafe nint DispatchMessage(Message* message);

	[DllImport("user32", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
	public static extern uint PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32", EntryPoint = "RegisterWindowMessageW", ExactSpelling = true, SetLastError = true)]
	private static extern unsafe uint RegisterWindowMessage(ushort* @string);

	public static unsafe uint RegisterWindowMessage(string name)
	{
		ushort* nameBuffer = stackalloc ushort[name.Length + 1];
		name.CopyTo(new(nameBuffer, name.Length));
		nameBuffer[name.Length] = 0;

		uint messageId = RegisterWindowMessage(nameBuffer);

		if (messageId == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		return messageId;
	}

	[DllImport("user32", EntryPoint = "PostQuitMessage", ExactSpelling = true, SetLastError = true)]
	public static extern void PostQuitMessage(int exitCode);

	public const int WmNull = 0x0000;
	public const int WmCreate = 0x0001;
	public const int WmDestroy = 0x0002;
	public const int WmSettingChange = 0x001A;
	public const int WmWindowsPosChanging = 0x0046;
	public const int WmClose = 0x0010;
	public const int WmContextMenu = 0x007B;
	public const int WmCommand = 0x0111;
	public const int WmTimer = 0x0113;
	public const int WmMenuCommand = 0x0126;
	public const int WmLeftButtonUp = 0x0202;
	public const int WmRightButtonUp = 0x0205;

	[DllImport("user32", EntryPoint = "GetWindowLongW", ExactSpelling = true, SetLastError = true)]
	public static extern int GetWindowLong(nint hwnd, int index);

	[DllImport("user32", EntryPoint = "SetWindowLongW", ExactSpelling = true, SetLastError = true)]
	public static extern int SetWindowLong(nint hwnd, int index, int newStyle);

	[DllImport("user32", EntryPoint = "MoveWindow", ExactSpelling = true, SetLastError = true)]
	public static extern int MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

	[DllImport("user32", EntryPoint = "GetCursorPos", ExactSpelling = true, SetLastError = true)]
	private static extern unsafe int GetCursorPos(Point* point);

	[StructLayout(LayoutKind.Sequential)]
	public struct Point
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Size
	{
		public int Width;
		public int Height;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
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

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe nint CreatePopupMenu();

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int SetMenuInfo(nint menuHandle, MenuInfo* menuInfo);

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int DestroyMenu(nint menuHandle);

	[DllImport("user32", EntryPoint = "InsertMenuItemW", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int InsertMenuItem(nint menuHandle, uint item, int isByPosition, MenuItemInfo* menuItemInfo);

	[DllImport("user32", EntryPoint = "SetMenuItemInfoW", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int SetMenuItemInfo(nint menuHandle, uint item, int isByPosition, MenuItemInfo* menuItemInfo);

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int RemoveMenu(nint menuHandle, uint position, RemoveMenuOptions options);

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	public static extern unsafe int TrackPopupMenuEx(nint menuHandle, TrackPopupMenuOptions options, int x, int y, nint windowHandle, TrackPopupMenuParameters* parameters);

	public struct TrackPopupMenuParameters
	{
		public int Size;
		public Rect ExcludedRectangle;
	}

	// To be used if we want to display a flyout at some point.
	//[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
	//public static extern unsafe int CalculatePopupWindowPosition(Point* anchorPoint, Size* size, TrackPopupMenuOptions options, Rect* excludeRect, Rect* popupWindowPosition);

	public struct MenuItemInfo
	{
		public int Size;
		public MenuItemFields Fields;
		public MenuItemType Type;
		public MenuItemState State;
		public uint Id;
		public nint SubMenuHandle;
		public nint CheckedBitmapHandle;
		public nint UncheckedBitmapHandle;
		public nint ItemData;
		public nint TypeData;
		public int CharacterCount;
		public nint BitmapHandle;
	}

	[Flags]
	public enum MenuItemFields : uint
	{
		State = 0x00000001,
		Id = 0x00000002,
		SubMenu = 0x00000004,
		CheckMarks = 0x00000008,
		LegacyTypeAndTypeData = 0x00000010,
		Data = 0x00000020,
		String = 0x00000040,
		Bitmap = 0x00000080,
		Type = 0x00000100,
	}

	[Flags]
	public enum MenuItemType : uint
	{
		String = 0x00000000,
		LegacyBitmap = 0x00000004,
		MenuBarBreak = 0x00000020,
		MenuBreak = 0x00000040,
		OwnerDraw = 0x00000100,
		RadioCheck = 0x00000200,
		Separator = 0x00000800,
		RightOrder = 0x00002000,
		RightJustify = 0x00004000,
	}

	[Flags]
	public enum MenuItemState : uint
	{
		None = 0x00000000,
		Grayed = 0x00000001,
		Disabled = 0x00000002,
		Checked = 0x00000008,
		Highlighted = 0x00000080,
		Default = 0x00001000,
	}

	public static class MenuItemBitmap
	{
		public const nint Callback = -1;
		public const nint System = 1;
		public const nint Restore = 2;
		public const nint Minimize = 3;
		public const nint Close = 5;
		public const nint CloseDisabled = 6;
		public const nint MinimizeDisabled = 7;
		public const nint PopupClose = 8;
		public const nint PopupRestore = 9;
		public const nint PopupMaximize = 10;
		public const nint PopupMinimize = 11;
	}

	[Flags]
	public enum RemoveMenuOptions : uint
	{
		ByCommand = 0x00000000,
		ByPosition = 0x00000400,
	}

	[Flags]
	public enum TrackPopupMenuOptions : uint
	{
		Recurse = 0x00000001,

		LeftButton = 0x00000000,
		RightButton = 0x00000002,

		LeftAlign = 0x00000000,
		CenterAlign = 0x00000004,
		RightAlign = 0x00000008,

		TopAlign = 0x00000000,
		VerticalCenterAlign = 0x00000010,
		BottomAlign = 0x00000020,

		Horizontal = 0x00000000,
		Vertical = 0x00000040,

		NoNotify = 0x00000080,
		ReturnCommand = 0x00000100,

		HorizontalPositiveAnimation = 0x00000400,
		HorizontalNegativeAnimation = 0x00000800,
		VerticalPositiveAnimation = 0x00001000,
		VerticalNegativeAnimation = 0x00002000,
		NoAnimation = 0x00004000,

		LayoutRightToLeft = 0x00008000,

		WorkArea = 0x00010000,
	}

	public struct MenuInfo
	{
		public int Size;
		public MenuInfoFields Fields;
		public MenuStyles Style;
		public int MaxHeight;
		public nint BackgroundBrush;
		public uint ContextHelpId;
		public nint MenuData;
	}

	[Flags]
	public enum MenuInfoFields : uint
	{
		MaxHeight = 0x00000001,
		Background = 0x00000002,
		HelpId = 0x00000004,
		MenuData = 0x00000008,
		Style = 0x00000010,
		ApplyToSubMenus = 0x80000000,
	}

	[Flags]
	public enum MenuStyles : uint
	{
		CheckOrBitmap = 0x04000000,
		NotifyByPosition = 0x08000000,
		AutoDismiss = 0x10000000,
		DragAndDrop = 0x20000000,
		Modeless = 0x40000000,
		NoCheck = 0x80000000,
	}

	[DllImport("uxtheme", EntryPoint = "#135", SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern int SetPreferredAppMode(int preferredAppMode);

	[DllImport("user32", EntryPoint = "SetTimer", SetLastError = true)]
	public static extern nuint SetTimer(nint windowHandle, nuint eventId, uint time, nint timerCallback);

	[DllImport("user32", EntryPoint = "KillTimer", SetLastError = true)]
	public static extern uint KillTimer(nint windowHandle, nuint eventId);

	[DllImport("user32", EntryPoint = "GetDoubleClickTime", SetLastError = true)]
	public static extern uint GetDoubleClickTime();
}
#pragma warning restore CS0649
