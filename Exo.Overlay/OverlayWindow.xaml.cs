using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using DeviceTools.DisplayDevices;
using static Exo.Overlay.NativeMethods;

namespace Exo.Overlay;

internal partial class OverlayWindow : Window
{
	private static void SetWindowExTransparent(IntPtr hwnd) => SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x00000020);

	private IntPtr _windowHandle;
	private Rectangle _absoluteBounds;

	public OverlayWindow()
	{
		InitializeComponent();
		DataContext = new OverlayViewModel();
		IsVisibleChanged += OverlayWindow_IsVisibleChanged;
	}

	private void SetWindowBounds()
		=> MoveWindow(_windowHandle, _absoluteBounds.Left, _absoluteBounds.Top, _absoluteBounds.Width, _absoluteBounds.Height, 1);

	private void OverlayWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		const uint Size = 200;

		if (e.NewValue is true)
		{
			// The code below bypasses WPF for positioning the Window.
			// Positioning Windows using WPF APIs is very broken on multi-monitor systems with a mix of different per-monitor DPIs.
			// See https://github.com/dotnet/wpf/issues/4127 for the issue.
			// NB: The code here should address that, but it has not been tested under these conditions.
			var point = GetCursorPos();
			var monitor = LogicalMonitor.GetNearestFromPoint(point.X, point.Y);
			var dpi = monitor.GetDpi();
			var bounds = monitor.GetMonitorInformation().MonitorArea;
			int width = (int)(Size * dpi.Horizontal / 96);
			int height = (int)(Size * dpi.Vertical / 96);
			int left = bounds.Left + (int)((uint)(bounds.Width - width) / 2);
			int top = bounds.Top + (int)(7 * (uint)(bounds.Height - height) / 8);
			_absoluteBounds = new(left, top, width, height);
			if (_windowHandle != 0)
			{
				SetWindowBounds();
			}
			else
			{
				Left = left * 96d / dpi.Horizontal;
				Top = top * 96d / dpi.Vertical;
			}
		}
	}

	protected override void OnInitialized(EventArgs e) => base.OnInitialized(e);

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);

		_windowHandle = new WindowInteropHelper(this).Handle;

		// Transparent background only works for the Window itself.
		// For some reason, the other controls still intercept clicks, so we need to use this interop code 🙁
		SetWindowExTransparent(_windowHandle);
		if (_absoluteBounds.Width > 0)
		{
			SetWindowBounds();
		}
	}
}
