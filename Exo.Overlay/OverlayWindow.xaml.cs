using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Exo.Overlay;

internal partial class OverlayWindow : Window
{
	[DllImport("user32")]
	[SuppressUnmanagedCodeSecurity]
	private static extern int GetWindowLong(IntPtr hwnd, int index);

	[DllImport("user32")]
	[SuppressUnmanagedCodeSecurity]
	private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

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

	public OverlayWindow()
	{
		InitializeComponent();
		DataContext = new OverlayViewModel();
		IsVisibleChanged += OverlayWindow_IsVisibleChanged;
	}

	private void OverlayWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		const int Size = 200;

		if (e.NewValue is true)
		{
			GetCursorPos(out var point);
			var currentScreen = Screen.FromPoint(new System.Drawing.Point(point.X, point.Y));
			int left = currentScreen.Bounds.Left + (currentScreen.Bounds.Width - (int)Width) / 2;
			int top = currentScreen.Bounds.Top + 2 * (currentScreen.Bounds.Height - (int)Height) / 3;
			//Left = left;
			//Top = top;
		}
		else
		{
		}
	}

	protected override void OnInitialized(EventArgs e) => base.OnInitialized(e);

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		// Transparent background only works for the Window itself.
		// For some reason, the other controls still intercept clicks, so we need to use this interop code 🙁
		SetWindowExTransparent(new WindowInteropHelper(this).Handle);
	}
}
