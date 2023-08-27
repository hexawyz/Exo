using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DeviceTools.DisplayDevices;

namespace Exo.Overlay;

internal partial class OverlayWindow : Window
{
	[DllImport("user32")]
	[SuppressUnmanagedCodeSecurity]
	private static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32")]
	[SuppressUnmanagedCodeSecurity]
	private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

	[DllImport("user32")]
	[SuppressUnmanagedCodeSecurity]
	private static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out Point point);

	[StructLayout(LayoutKind.Sequential)]
	private struct Point
	{
		public int X;
		public int Y;
	}

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
			GetCursorPos(out var point);
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
