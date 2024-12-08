using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using static Exo.Overlay.NativeMethods;

namespace Exo.Overlay;

internal sealed class NotificationWindow : SynchronizationContext, IDisposable
{
	private abstract class RegisteredCallback
	{
		public ExecutionContext? ExecutionContext { get; }

		public RegisteredCallback(ExecutionContext? executionContext)
		{
			ExecutionContext = executionContext;
		}

		public void Execute()
		{
			if (ExecutionContext is not null)
			{
				ExecutionContext.Run(ExecutionContext, ExecutionContextCallbackMarshaller.Callback, this);
			}
			else
			{
				UnsafeExecute();
			}
		}

		public abstract void UnsafeExecute();
	}

	private sealed class RegisteredSendOrPostCallback : RegisteredCallback
	{
		private readonly SendOrPostCallback _callback;
		private readonly object? _state;

		public RegisteredSendOrPostCallback(ExecutionContext? executionContext, SendOrPostCallback callback, object? state)
			: base(executionContext)
		{
			_callback = callback;
			_state = state;
		}

		public override void UnsafeExecute() => _callback(_state);
	}

	private sealed class RegisteredActionCallback : RegisteredCallback
	{
		private readonly Action _callback;

		public RegisteredActionCallback(ExecutionContext? executionContext, Action callback)
			: base(executionContext)
		{
			_callback = callback;
		}

		public override void UnsafeExecute() => _callback();
	}

	private sealed class ExecutionContextCallbackMarshaller
	{
		public static readonly ContextCallback Callback = new ContextCallback(new ExecutionContextCallbackMarshaller().Invoke);

		private ExecutionContextCallbackMarshaller() { }

		private void Invoke(object? state) => Invoke(Unsafe.As<RegisteredCallback>(state!));
		private void Invoke(RegisteredCallback state) => state.UnsafeExecute();
	}

	public readonly struct SwitchToAwaitable
	{
		private readonly NotificationWindow _notificationWindow;

		internal SwitchToAwaitable(NotificationWindow notificationWindow) => _notificationWindow = notificationWindow;

		public SwitchToAwaiter GetAwaiter() => new SwitchToAwaiter(_notificationWindow);
	}

	public readonly struct SwitchToAwaiter : INotifyCompletion, ICriticalNotifyCompletion
	{
		private readonly NotificationWindow _notificationWindow;

		internal SwitchToAwaiter(NotificationWindow notificationWindow) => _notificationWindow = notificationWindow;

		public void OnCompleted(Action continuation) => _notificationWindow.RegisterCallback(continuation, true);

		public void UnsafeOnCompleted(Action continuation) => _notificationWindow.RegisterCallback(continuation, false);

		public bool IsCompleted => Thread.CurrentThread == _notificationWindow._messageThread;

		public void GetResult() { }
	}

	private static readonly WeakReference InstanceReference = new(null!, false);

	private static readonly ushort WindowClass = CreateWindowClass();

	private static readonly uint TaskbarCreatedMessageId = RegisterWindowMessage("TaskbarCreated");
	public const uint IconMessageId = 0x8000; // WM_APP
	public const uint CallbackMessageId = 0x8001; // WM_APP + 1
	public const uint RegisterIconMessageId = 0x8002; // WM_APP + 2

	private static unsafe ushort CreateWindowClass()
	{
		const string ClassName = "Exo_Notification_Window";
		ushort* className = stackalloc ushort[ClassName.Length + 1];
		ClassName.CopyTo(new(className, ClassName.Length));
		className[ClassName.Length] = 0;

		var windowClassDefinition = new WindowClassEx
		{
			Size = sizeof(WindowClassEx),
			WindowProcedure = &WindowProcedure,
			ClassName = className,
			InstanceHandle = GetModuleHandle(0)
		};

		ushort result = RegisterClassEx(&windowClassDefinition);

		if (result == 0) throw new Win32Exception(Marshal.GetLastWin32Error());

		return result;
	}

	[UnmanagedCallersOnly]
	private static unsafe IntPtr WindowProcedure(nint windowHandle, uint message, nint wParam, nint lParam)
	{
		// Always exit the message loop when the window is destroyed.
		if (message == WmDestroy)
		{
			PostQuitMessage(0);
		}

		if (message == WmCreate)
		{
			HandleWindowCreation(windowHandle, in *(WindowCreationParameters*)lParam);
		}

		if (TryGetSharedInstance() is { } w && w.Handle == windowHandle)
		{
			return w.WindowProcedure(message, wParam, lParam);
		}
		else
		{
			return DefWindowProc(windowHandle, message, wParam, lParam);
		}
	}

	private static void HandleWindowCreation(nint handle, in WindowCreationParameters parameters)
	{
		if (parameters.CreateParameters == 0) return;
		var gcHandle = GCHandle.FromIntPtr(parameters.CreateParameters);
		if (gcHandle.Target is Tuple<NotificationWindow, TaskCompletionSource<NotificationWindow>> t)
		{
			Volatile.Write(ref t.Item1._handle, handle);
			t.Item2.TrySetResult(t.Item1);
		}
	}

	private static NotificationWindow? TryGetSharedInstance()
	{
		var target = InstanceReference.Target;

		if (target is not null)
		{
			if (target is NotificationWindow w)
			{
				return w;
			}
			else if (target is TaskCompletionSource<NotificationWindow> { Task: { IsCompletedSuccessfully: true } } tcs)
			{
				return tcs.Task.Result;
			}
		}
		return null;
	}

	private static unsafe void MessageLoop(object? state)
	{
		if (state is not Tuple<NotificationWindow, TaskCompletionSource<NotificationWindow>>) throw new InvalidOperationException();
		MessageLoop(Unsafe.As<object, Tuple<NotificationWindow, TaskCompletionSource<NotificationWindow>>>(ref state));
	}

	private static void MessageLoop(Tuple<NotificationWindow, TaskCompletionSource<NotificationWindow>> state) => state.Item1.TryCreateWindowAndRunMessageLoop(state.Item2);

	private static readonly ParameterizedThreadStart MessageLoopThreadProcedure = MessageLoop;

	public static ValueTask<NotificationWindow> GetOrCreateAsync()
	{
		lock (InstanceReference)
		{
			var notificationWindowOrTcs = InstanceReference.Target;

			if (notificationWindowOrTcs is NotificationWindow window)
			{
				return new(window);
			}
			else
			{
				if (notificationWindowOrTcs is not TaskCompletionSource<NotificationWindow> tcs)
				{
					tcs = new();
					InstanceReference.Target = tcs;
					window = new NotificationWindow();
					window._messageThread.Start(Tuple.Create(window, tcs));
				}
				return new(tcs.Task);
			}
		}
	}

	private readonly Thread _messageThread;
	private readonly ConcurrentQueue<RegisteredCallback> _pendingCallbacks;
	private readonly Dictionary<ushort, WeakReference<NotifyIcon>> _registeredIcons;
	private readonly Dictionary<nint, WeakReference<PopupMenu>> _registeredMenus;
	private nint _handle;
	private int _isDisposed;
	private int _doubleClickDelay;
	private int _clickTimerNotifyIconId;
	private uint _clickTimerClickPosition;

	public nint Handle => _handle;

	public bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

	private NotificationWindow()
	{
		_messageThread = new(MessageLoopThreadProcedure) { Name = "NotificationWindow", IsBackground = true };
		_pendingCallbacks = new();
		_registeredIcons = new();
		_registeredMenus = new();
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
		PostMessage(_handle, WmClose, 0, 0);
	}

	[Conditional("DEBUG")]
	internal void EnforceThreadSafety()
	{
		if (Thread.CurrentThread != _messageThread)
		{
			throw new InvalidOperationException("All accesses must be synchronized using the NotificationWindow SynchronizationContext.");
		}
	}

	public NotifyIcon CreateNotifyIcon(ushort iconId, int iconResourceId, string tooltipText)
	{
		EnforceThreadSafety();
		if (_registeredIcons.TryGetValue(iconId, out var wr) && wr.TryGetTarget(out _))
		{
			throw new InvalidOperationException("An icon with the same ID has already been registered.");
		}

		var icon = new NotifyIcon(this, iconId, iconResourceId, tooltipText);
		if (wr is not null) wr.SetTarget(icon);
		else _registeredIcons.Add(iconId, new WeakReference<NotifyIcon>(icon));

		try
		{
			icon.CreateIcon();
		}
		catch
		{
		}

		return icon;
	}

	public PopupMenu CreatePopupMenu() => CreatePopupMenu([]);

	public PopupMenu CreatePopupMenu(params MenuItem[] items)
	{
		EnforceThreadSafety();
		var menu = new PopupMenu(this);
		try
		{
			if (items is not null)
			{
				foreach (var item in items)
				{
					menu.MenuItems.Add(item);
				}
			}
		}
		catch
		{
			menu.Dispose();
			throw;
		}
		_registeredMenus.Add(menu.Handle, new(menu, false));

		return menu;
	}

	internal void UnregisterIcon(NotifyIcon icon) => _registeredIcons.Remove(icon.IconId);
	internal void UnregisterMenu(PopupMenu menu) => _registeredMenus.Remove(menu.Handle);

	private bool TryCreateWindow(TaskCompletionSource<NotificationWindow> taskCompletionSource)
	{
		// Create a GC Handle to pass information to the window procedure for completing the creation process.
		// Some messages may still be missed, but this will allow processing all messages, and the handle should be assigned before 
		var tuple = Tuple.Create(this, taskCompletionSource);
		var gcHandle = GCHandle.Alloc(tuple);

		var handle = CreateWindowEx
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
			GetModuleHandle(0),
			(nint)gcHandle
		);

		gcHandle.Free();

		if (handle == IntPtr.Zero)
		{
			taskCompletionSource.SetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Win32Exception(Marshal.GetLastWin32Error())));

			lock (InstanceReference)
			{
				InstanceReference.Target = null;
			}

			Dispose();

			return false;
		}

		lock (InstanceReference)
		{
			InstanceReference.Target = this;
		}

		return true;
	}

	private void TryCreateWindowAndRunMessageLoop(TaskCompletionSource<NotificationWindow> taskCompletionSource)
	{
		var previous = Current;
		SetSynchronizationContext(this);
		_doubleClickDelay = (int)GetDoubleClickTime();
		_clickTimerNotifyIconId = -1;
		try
		{
			if (!TryCreateWindow(taskCompletionSource)) return;
			MessageLoop();
		}
		finally
		{
			ProcessRemainingCallbacks();
			SetSynchronizationContext(previous);
		}
	}

	private unsafe void MessageLoop()
	{
		Unsafe.SkipInit(out Message message);
		while (GetMessage(&message, IntPtr.Zero, 0, 0) > 0)
		{
			if (message.MessageId == CallbackMessageId)
			{
				ProcessCallback();
			}
			else
			{
				TranslateMessage(&message);
				DispatchMessage(&message);
			}
		}
	}

	private void ProcessCallback()
	{
		// NB: There is no good reason why this call would ever return false (unless somebody is messing with the messages), but we have to handle the result anyway.
		if (_pendingCallbacks.TryDequeue(out var callback))
		{
			ProcessCallback(callback);
		}
	}

	private void ProcessRemainingCallbacks()
	{
		while (_pendingCallbacks.TryDequeue(out var callback))
		{
			ProcessCallback(callback);
		}
	}

	private static void ProcessCallback(RegisteredCallback callback)
	{
		try
		{
			callback.Execute();
		}
		catch
		{
		}
	}

	private unsafe nint WindowProcedure(uint message, nint wParam, nint lParam)
	{
		WeakReference<NotifyIcon>? wr;
		NotifyIcon? icon;
		switch (message)
		{
		case WmSettingChange:
			_doubleClickDelay = (int)GetDoubleClickTime();
			goto MessageProcessed;
		case WmTimer:
			if (wParam == _clickTimerNotifyIconId)
			{
				if (!_registeredIcons.TryGetValue((ushort)wParam, out wr) || !wr.TryGetTarget(out icon)) break;
				_clickTimerNotifyIconId = -1;
				wParam = (nint)(nuint)_clickTimerClickPosition;
				_clickTimerClickPosition = 0;
				goto HandleMenu;
			}
			goto MessageProcessed;
		case WmMenuCommand:
			HandleMenuCommand(lParam, (int)wParam);
			goto MessageProcessed;
		case IconMessageId:
			if (!_registeredIcons.TryGetValue((ushort)(lParam >>> 16), out wr) || !wr.TryGetTarget(out icon)) break;
			switch ((ushort)lParam)
			{
			case WmLeftButtonUp:
				if (_clickTimerNotifyIconId >= 0)
				{
					KillTimer(_handle, (uint)_clickTimerNotifyIconId);
					if (icon.IconId == _clickTimerNotifyIconId)
					{
						_clickTimerNotifyIconId = -1;
						_clickTimerClickPosition = 0;
						icon.OnDoubleClick();
						goto MessageProcessed;
					}
				}
				_clickTimerNotifyIconId = icon.IconId;
				_clickTimerClickPosition = (uint)wParam;
				SetTimer(_handle, (uint)_clickTimerNotifyIconId, (uint)_doubleClickDelay, 0);
				goto MessageProcessed;
			case WmContextMenu:
			case WmRightButtonUp:
				if (_clickTimerNotifyIconId >= 0)
				{
					KillTimer(_handle, (uint)_clickTimerNotifyIconId);
					_clickTimerNotifyIconId = -1;
					_clickTimerClickPosition = 0;
				}
				goto HandleMenu;
			}
			goto MessageProcessed;
		}
		return DefWindowProc(_handle, message, wParam, lParam);

	HandleMenu:;
		SetForegroundWindow(_handle);
		var result = TrackPopupMenuEx
		(
			icon.ContextMenu.Handle,
			TrackPopupMenuOptions.RightAlign | TrackPopupMenuOptions.BottomAlign | TrackPopupMenuOptions.RightButton,
			(short)(ushort)wParam,
			(short)(ushort)(wParam >>> 16),
			_handle,
			null
		);
		PostMessage(_handle, WmNull, 0, 0);

	MessageProcessed:;
		return 0;
	}

	private void HandleMenuCommand(nint menuHandle, int itemIndex)
	{
		if (!_registeredMenus.TryGetValue(menuHandle, out var wr) || !wr.TryGetTarget(out var menu)) return;

		try
		{
			menu.OnClick(itemIndex);
		}
		catch
		{
		}
	}

	public override void Post(SendOrPostCallback d, object? state)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, typeof(NotificationWindow));
		_pendingCallbacks.Enqueue(new RegisteredSendOrPostCallback(ExecutionContext.Capture(), d, state));
		if (PostMessage(_handle, CallbackMessageId, 0, 0) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

	private void RegisterCallback(Action callback, bool shouldFlowContext)
	{
		ObjectDisposedException.ThrowIf(IsDisposed, typeof(NotificationWindow));
		_pendingCallbacks.Enqueue(new RegisteredActionCallback(shouldFlowContext ? ExecutionContext.Capture() : null, callback));
		if (PostMessage(_handle, CallbackMessageId, 0, 0) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	public SwitchToAwaitable SwitchTo() => new(this);
}
