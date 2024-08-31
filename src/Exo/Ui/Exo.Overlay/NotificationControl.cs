namespace Exo.Overlay;

internal abstract class NotificationControl : IDisposable
{
	private NotificationWindow? _notificationWindow;

	public NotificationWindow NotificationWindow => _notificationWindow ?? throw new ObjectDisposedException(GetType().FullName);

	protected NotificationControl(NotificationWindow notificationWindow)
	{
		_notificationWindow = notificationWindow;
	}

	public unsafe void Dispose()
	{
		if (Volatile.Read(ref _notificationWindow) is not { } notificationWindow) return;

		notificationWindow.EnforceThreadSafety();

		Volatile.Write(ref _notificationWindow, null);

		DisposeCore(notificationWindow);
	}

	protected virtual void DisposeCore(NotificationWindow notificationWindow)
	{
	}

	protected void EnsureNotDisposed()
	{
		if (Volatile.Read(ref _notificationWindow) is null) throw new ObjectDisposedException(GetType().FullName);
	}
}
