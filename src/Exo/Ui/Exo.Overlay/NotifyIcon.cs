using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Exo.Overlay.NativeMethods;

namespace Exo.Overlay;

/// <summary>Represents an icon in the notification area.</summary>
/// <remarks>Instances of this class must be created and accessed through the <see cref="NotificationWindow"/> synchronization context.</remarks>
internal sealed class NotifyIcon : NotificationControl
{
	public PopupMenu ContextMenu { get; }
	// NB: We could use GUIDs to register the icons, but that solution comes with problems, so for now, stick with regular icon IDs. (Related to code signature and/or executable path.)
	//private readonly Guid _iconId;
	private readonly ushort _iconId;
	private readonly int _iconResourceId;
#pragma warning disable IDE0044 // Add readonly modifier
	private nint _iconHandle;
	private bool _isVisible;
	private string _tooltipText;
#pragma warning restore IDE0044 // Add readonly modifier
	private bool _isCreated;

	public event EventHandler DoubleClick;

	internal ushort IconId => _iconId;

	internal NotifyIcon(NotificationWindow notificationWindow, ushort iconId, int iconResourceId, string tooltipText)
		: base(notificationWindow)
	{
		_iconId = iconId;
		_iconResourceId = iconResourceId;
		_iconHandle = LoadIconMetric(GetModuleHandle(0), _iconResourceId, IconMetric.Small);
		_tooltipText = tooltipText;
		ContextMenu = notificationWindow.CreatePopupMenu();
	}

	protected override unsafe void DisposeCore(NotificationWindow notificationWindow)
	{
		notificationWindow.UnregisterIcon(this);

		if (_isCreated)
		{
			var iconData = new NotifyIconData
			{
				Size = Unsafe.SizeOf<NotifyIconData>(),
				WindowHandle = notificationWindow.Handle,
				IconId = _iconId,
				Features = 0,
			};

			Shell_NotifyIcon(NotifyIconMessage.Delete, &iconData);
		}
	}

	internal unsafe void CreateIconIfNotCreated()
	{
		if (_isCreated) return;
		CreateIcon();
	}

	internal unsafe void CreateIcon()
	{
		var iconData = new NotifyIconData
		{
			Size = Unsafe.SizeOf<NotifyIconData>(),
			WindowHandle = NotificationWindow.Handle,
			IconId = _iconId,
			Features = NotifyIconFeatures.Message | NotifyIconFeatures.Icon | NotifyIconFeatures.Tip | NotifyIconFeatures.ShowTip,
			IconHandle = _iconHandle,
			CallbackMessage = NotificationWindow.IconMessageId,
		};

		MemoryMarshal.Cast<char, ushort>(_tooltipText.AsSpan()).CopyTo(iconData.TipText);

		if (Shell_NotifyIcon(NotifyIconMessage.Add, &iconData) == 0)
		{
			return;
		}

		iconData.Features = NotifyIconFeatures.Guid;
		iconData.Version = 4;

		if (Shell_NotifyIcon(NotifyIconMessage.SetVersion, &iconData) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		_isCreated = true;
	}

	private unsafe void UpdateIcon(NotifyIconFeatures changedFeatures)
	{
		EnsureNotDisposed();

		var iconData = new NotifyIconData
		{
			Size = Unsafe.SizeOf<NotifyIconData>(),
			WindowHandle = NotificationWindow.Handle,
			IconId = _iconId,
			Features = NotifyIconFeatures.ShowTip | changedFeatures,
			CallbackMessage = 0x8000,
		};

		if ((changedFeatures & NotifyIconFeatures.Icon) != 0)
		{
			iconData.IconHandle = _iconHandle;
		}

		if ((changedFeatures & NotifyIconFeatures.Tip) != 0)
		{
			MemoryMarshal.Cast<char, ushort>(_tooltipText.AsSpan()).CopyTo(iconData.TipText);
		}

		if ((changedFeatures & NotifyIconFeatures.State) != 0)
		{
			iconData.State = _isVisible ? 0 : NotifyIconStates.Hidden;
			iconData.StateMask = NotifyIconStates.Hidden;
		}

		if (Shell_NotifyIcon(NotifyIconMessage.Modify, &iconData) == 0)
		{
			Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}
	}

	internal void OnDoubleClick() => DoubleClick?.Invoke(this, EventArgs.Empty);
}
