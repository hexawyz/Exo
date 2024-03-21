using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using Exo.DeviceNotifications;
using Exo.Services;
using Microsoft.Win32.SafeHandles;

namespace Exo.Service;

/// <summary>A notification window to replace service notifications when the app is not run as a service.</summary>
/// <remarks>This code is not expected to be used in the release version.</remarks>
internal sealed class NotificationWindow : IDeviceNotificationService, IDisposable
{
	[DllImport("kernel32", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern IntPtr GetModuleHandleW(IntPtr zero);

	private unsafe struct WindowClassEx
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

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern ushort RegisterClassExW(in WindowClassEx param);

	[DllImport("user32", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern IntPtr CreateWindowExW(uint exStyle, IntPtr className, string windowName, uint style, int x, int y, int width, int height, IntPtr parentWindowHandle, IntPtr menuHandle, IntPtr instanceHandle, IntPtr param);

	[DllImport("user32", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern uint DestroyWindow(IntPtr windowHandle);

	[DllImport("user32", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern IntPtr DefWindowProcW(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

	private struct Message
	{
		public IntPtr WindowHandle;
		public uint MessageId;
		public IntPtr WParam;
		public IntPtr LParam;
		public uint Time;
		public Point Point;
		public uint Private;
	}

	private struct Point
	{
		public int X;
		public int Y;
	}

	[DllImport("user32", EntryPoint = "GetMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern uint GetMessage(out Message message, IntPtr windowHandle, uint messageFilterMin, uint messageFilterMax);

	[DllImport("user32", EntryPoint = "TranslateMessage", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern uint TranslateMessage(ref Message message);

	[DllImport("user32", EntryPoint = "DispatchMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern IntPtr DispatchMessage(ref Message message);

	[DllImport("user32", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern uint PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

	[DllImport("user32", EntryPoint = "PostQuitMessage", ExactSpelling = true, SetLastError = true)]
	[SuppressUnmanagedCodeSecurity]
	private static extern void PostQuitMessage(int exitCode);

	private static readonly ConcurrentDictionary<IntPtr, WeakReference<NotificationWindow>> NotificationWindows = new();

	private static readonly ushort WindowClass = CreateWindowClass();

	private static unsafe ushort CreateWindowClass()
	{
		const string ClassName = "DBG_Exo_DeviceNotificationWindow";
		fixed (char* classNamePointer = ClassName)
		{
			ushort result = RegisterClassExW
			(
				new WindowClassEx
				{
					Size = sizeof(WindowClassEx),
					WindowProcedure = &WindowProcedure,
					ClassName = classNamePointer,
					InstanceHandle = GetModuleHandleW(IntPtr.Zero)
				}
			);

			if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

			return result;
		}
	}

	[UnmanagedCallersOnly]
	private static IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
	{
		// Always exit the message loop when the window is destroyed.
		if (message == WmDestroy)
		{
			PostQuitMessage(0);
		}
		else if (message == WmDeviceChange)
		{
			if (NotificationWindows.TryGetValue(windowHandle, out var weakReference) && weakReference.TryGetTarget(out var window))
			{
				return (IntPtr)window._deviceNotificationEngine!.HandleNotification((int)wParam, lParam);
			}
		}

		return DefWindowProcW(windowHandle, message, wParam, lParam);
	}

	private static void MessageLoop(object? state)
	{
		if (state is not Tuple<NotificationWindow, TaskCompletionSource>) throw new InvalidOperationException();

		if (!Unsafe.As<object, Tuple<NotificationWindow, TaskCompletionSource>>(ref state).Item1.TryCreateWindow(Unsafe.As<object, Tuple<NotificationWindow, TaskCompletionSource>>(ref state).Item2)) return;

		state = null;

		Message message;
		while (GetMessage(out message, IntPtr.Zero, 0, 0) > 0)
		{
			TranslateMessage(ref message);
			DispatchMessage(ref message);
		}
	}

	private static readonly ParameterizedThreadStart MessageLoopThreadProcedure = MessageLoop;

	private const int WmDestroy = 0x0002;
	private const int WmClose = 0x0010;
	private const int WmDeviceChange = 0x0219;

	private readonly Thread _messageThread = new(MessageLoopThreadProcedure);
	private IntPtr _handle;
	private DeviceNotificationEngine? _deviceNotificationEngine;
	private int _isDisposed;

	public NotificationWindow()
	{
		var tcs = new TaskCompletionSource();
		_messageThread.Start(Tuple.Create(this, tcs));
		tcs.Task.GetAwaiter().GetResult();
	}

	private bool TryCreateWindow(TaskCompletionSource taskCompletionSource)
	{
		var handle = CreateWindowExW
		(
			0,
			(IntPtr)WindowClass,
			"Exo Device Notification Window (DEBUG)",
			0,
			0,
			0,
			0,
			0,
			IntPtr.Zero,
			IntPtr.Zero,
			GetModuleHandleW(IntPtr.Zero),
			IntPtr.Zero
		);

		if (handle == IntPtr.Zero)
		{
			taskCompletionSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Win32Exception(Marshal.GetLastWin32Error())));
			return false;
		}

		_deviceNotificationEngine = DeviceNotificationEngine.CreateForWindow(handle);

		NotificationWindows.TryAdd(handle, new WeakReference<NotificationWindow>(this));

		Volatile.Write(ref _handle, handle);

		taskCompletionSource.TrySetResult();

		return true;
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private void Dispose(bool disposing)
	{
		if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0) return;

		if (_handle == IntPtr.Zero) return;

		if (disposing)
		{
			Interlocked.Exchange(ref _deviceNotificationEngine, null)?.Dispose();
		}

		if (_handle != IntPtr.Zero)
		{
			NotificationWindows.TryRemove(_handle, out _);
			// WM_CLOSE will trigger DestroyWindow which will send WM_DESTROY which will send WM_QUIT which will end the message loop.
			PostMessage(_handle, WmClose, IntPtr.Zero, IntPtr.Zero);
			// We could wait for the thread to end here, but not in all cases, so that's not worth it.
		}
	}

	public IDisposable RegisterDeviceNotifications<T>(SafeFileHandle deviceFileHandle, T state, IDeviceHandleNotificationSink<T> sink)
		=> _deviceNotificationEngine!.RegisterDeviceNotifications(deviceFileHandle, state, sink);

	public IDisposable RegisterDeviceNotifications(Guid deviceInterfaceClassGuid, IDeviceNotificationSink sink)
		=> _deviceNotificationEngine!.RegisterDeviceNotifications(deviceInterfaceClassGuid, sink);

	public IDisposable RegisterDeviceNotifications(IDeviceNotificationSink sink)
		=> _deviceNotificationEngine!.RegisterDeviceNotifications(sink);
}
