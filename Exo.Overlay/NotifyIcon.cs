using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Exo.Overlay;

internal sealed class NotifyIcon : IDisposable
{
	private sealed class NotificationWindow : IDisposable
	{
		private static readonly ushort WindowClass = CreateWindowClass();

		private static unsafe ushort CreateWindowClass()
		{
			const string ClassName = "Exo_NotifyIcon_Window";
			fixed (char* classNamePointer = ClassName)
			{
				var windowClassDefinition = new NativeMethods.WindowClassEx
				{
					Size = sizeof(NativeMethods.WindowClassEx),
					WindowProcedure = &WindowProcedure,
					ClassName = classNamePointer,
					InstanceHandle = NativeMethods.GetModuleHandle(IntPtr.Zero)
				};

				ushort result = NativeMethods.RegisterClassEx(&windowClassDefinition);

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
				NativeMethods.PostQuitMessage(0);
			}

			return NativeMethods.DefWindowProc(windowHandle, message, wParam, lParam);
		}

		private static unsafe void MessageLoop(object? state)
		{
			if (state is not Tuple<NotificationWindow, TaskCompletionSource>) throw new InvalidOperationException();

			if (!Unsafe.As<object, Tuple<NotificationWindow, TaskCompletionSource>>(ref state).Item1.TryCreateWindow(Unsafe.As<object, Tuple<NotificationWindow, TaskCompletionSource>>(ref state).Item2)) return;

			state = null;

			Unsafe.SkipInit(out NativeMethods.Message message);

			while (NativeMethods.GetMessage(&message, IntPtr.Zero, 0, 0) > 0)
			{
				NativeMethods.TranslateMessage(&message);
				NativeMethods.DispatchMessage(&message);
			}
		}

		private static readonly ParameterizedThreadStart MessageLoopThreadProcedure = MessageLoop;

		private const int WmDestroy = 0x0002;
		private const int WmClose = 0x0010;

		private readonly Thread _messageThread = new(MessageLoopThreadProcedure);
		private nint _handle;
		private int _isDisposed;

		public nint Handle => _handle;

		public NotificationWindow()
		{
			var tcs = new TaskCompletionSource();
			_messageThread.Start(Tuple.Create(this, tcs));
			tcs.Task.GetAwaiter().GetResult();
		}

		~NotificationWindow() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) != 0) return;

			if (_handle == 0) return;

			// WM_CLOSE will trigger DestroyWindow which will send WM_DESTROY which will send WM_QUIT which will end the message loop.
			NativeMethods.PostMessage(_handle, WmClose, 0, 0);
		}

		private bool TryCreateWindow(TaskCompletionSource taskCompletionSource)
		{
			var handle = NativeMethods.CreateWindowEx
			(
				0,
				WindowClass,
				"Exo Notification Icon",
				0,
				0,
				0,
				0,
				0,
				0,
				0,
				NativeMethods.GetModuleHandle(0),
				0
			);

			if (handle == IntPtr.Zero)
			{
				taskCompletionSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Win32Exception(Marshal.GetLastWin32Error())));
				return false;
			}

			Volatile.Write(ref _handle, handle);

			taskCompletionSource.TrySetResult();

			return true;
		}
	}

	private static readonly WeakReference<NotificationWindow> SharedNotificationWindow = new(null!, false);
	private static readonly ConcurrentDictionary<Guid, WeakReference<NotifyIcon>> RegisteredIcons = new();

	private readonly NotificationWindow _notificationWindow;
	private WeakReference<NotifyIcon>? _weakReference;
	private readonly Guid _iconId;
	private readonly int _iconResourceId;
#pragma warning disable IDE0044 // Add readonly modifier
	private nint _iconHandle;
	private bool _isVisible;
	private string _tooltipText;
#pragma warning restore IDE0044 // Add readonly modifier

	private object Lock => Volatile.Read(ref _weakReference) ?? throw new ObjectDisposedException(nameof(NotifyIcon));

	public NotifyIcon(Guid iconId, int iconResourceId, string tooltipText)
	{
		_iconId = iconId;
		_iconResourceId = iconResourceId;
		_iconHandle = NativeMethods.LoadIconMetric(NativeMethods.GetModuleHandle(0), _iconResourceId, NativeMethods.IconMetric.Small);
		_tooltipText = tooltipText;

		_weakReference = RegisteredIcons.GetOrAdd(iconId, _ => new(null!, false));

		lock (_weakReference)
		{
			if (_weakReference.TryGetTarget(out _))
			{
				throw new InvalidOperationException("An icon with the same ID is already registered.");
			}

			lock (SharedNotificationWindow)
			{
				if (!SharedNotificationWindow.TryGetTarget(out _notificationWindow!))
				{
					SharedNotificationWindow.SetTarget(_notificationWindow = new NotificationWindow());
				}
			}

			CreateIcon();
		}
	}

	public unsafe void Dispose()
	{
		if (Interlocked.Exchange(ref _weakReference, null) is not { } wr) return;

		lock (wr)
		{
			wr.SetTarget(null!);

			var iconData = new NativeMethods.NotifyIconData
			{
				Size = Unsafe.SizeOf<NativeMethods.NotifyIconData>(),
				Features = NativeMethods.NotifyIconFeatures.Guid,
				ItemGuid = _iconId,
			};

			if (NativeMethods.Shell_NotifyIcon(NativeMethods.NotifyIconMessage.Delete, &iconData) == 0)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
		}
	}

	private unsafe void CreateIcon()
	{
		var iconData = new NativeMethods.NotifyIconData
		{
			Size = Unsafe.SizeOf<NativeMethods.NotifyIconData>(),
			WindowHandle = _notificationWindow.Handle,
			Features = NativeMethods.NotifyIconFeatures.Icon | NativeMethods.NotifyIconFeatures.Tip | NativeMethods.NotifyIconFeatures.Guid | NativeMethods.NotifyIconFeatures.ShowTip,
			IconHandle = _iconHandle,
			ItemGuid = _iconId,
			CallbackMessage = 0x8000,
		};

		MemoryMarshal.Cast<char, ushort>(_tooltipText.AsSpan()).CopyTo(iconData.TipText);

		if (NativeMethods.Shell_NotifyIcon(NativeMethods.NotifyIconMessage.Add, &iconData) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		iconData.Features = NativeMethods.NotifyIconFeatures.Guid;
		iconData.Version = 4;

		if (NativeMethods.Shell_NotifyIcon(NativeMethods.NotifyIconMessage.SetVersion, &iconData) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	private unsafe void UpdateIcon(NativeMethods.NotifyIconFeatures changedFeatures)
	{
		lock (Lock)
		{
			EnsureNotDisposed();

			var iconData = new NativeMethods.NotifyIconData
			{
				Size = Unsafe.SizeOf<NativeMethods.NotifyIconData>(),
				WindowHandle = _notificationWindow.Handle,
				Features = NativeMethods.NotifyIconFeatures.Guid | NativeMethods.NotifyIconFeatures.ShowTip | changedFeatures,
				ItemGuid = _iconId,
				CallbackMessage = 0x8000,
			};

			if ((changedFeatures & NativeMethods.NotifyIconFeatures.Icon) != 0)
			{
				iconData.IconHandle = _iconHandle;
			}

			if ((changedFeatures & NativeMethods.NotifyIconFeatures.Tip) != 0)
			{
				MemoryMarshal.Cast<char, ushort>(_tooltipText.AsSpan()).CopyTo(iconData.TipText);
			}

			if ((changedFeatures & NativeMethods.NotifyIconFeatures.State) != 0)
			{
				iconData.State = _isVisible ? 0 : NativeMethods.NotifyIconStates.Hidden;
				iconData.StateMask = NativeMethods.NotifyIconStates.Hidden;
			}

			if (NativeMethods.Shell_NotifyIcon(NativeMethods.NotifyIconMessage.Modify, &iconData) == 0)
			{
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
			}
		}
	}

	private void EnsureNotDisposed()
	{
		if (_weakReference is null) throw new ObjectDisposedException(nameof(NotifyIcon));
	}
}
