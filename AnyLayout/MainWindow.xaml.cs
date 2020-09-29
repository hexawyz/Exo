using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DeviceTools.RawInput;

namespace AnyLayout
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int WM_INPUT = 0xFF;

        private readonly PressedKeyViewModel _viewModel = new PressedKeyViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            var helper = new WindowInteropHelper(this);
            helper.EnsureHandle();
            var hwndSource = HwndSource.FromHwnd(helper.Handle);

            hwndSource.AddHook(WindowProc);

            NativeMethods.RegisterRawInputDevices
            (
                new[]
                {
                    new NativeMethods.RawInputDeviceRegisgtration
                    {
                        TargetHandle = hwndSource.Handle,
                        UsagePage = HidUsagePage.GenericDesktop,
                        Usage = (ushort)HidGenericDesktopUsage.Keyboard,
                        Flags = NativeMethods.RawInputDeviceFlags.NoLegacy | NativeMethods.RawInputDeviceFlags.AppKeys | NativeMethods.RawInputDeviceFlags.NoHotKeys
                    },
                    new NativeMethods.RawInputDeviceRegisgtration
                    {
                        TargetHandle = hwndSource.Handle,
                        UsagePage = HidUsagePage.Consumer,
                        Usage = (ushort)HidConsumerUsage.ConsumerControl,
                        Flags = NativeMethods.RawInputDeviceFlags.NoLegacy | NativeMethods.RawInputDeviceFlags.AppKeys | NativeMethods.RawInputDeviceFlags.NoHotKeys
                    },
                    new NativeMethods.RawInputDeviceRegisgtration
                    {
                        TargetHandle = hwndSource.Handle,
                        UsagePage = HidUsagePage.Consumer,
                        Usage = 0,
                        Flags = NativeMethods.RawInputDeviceFlags.NoLegacy | NativeMethods.RawInputDeviceFlags.AppKeys | NativeMethods.RawInputDeviceFlags.NoHotKeys | NativeMethods.RawInputDeviceFlags.PageOnly
                    }
                },
                1,
                (uint)Marshal.SizeOf<NativeMethods.RawInputDeviceRegisgtration>()
            );
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_INPUT)
            {
                var data = NativeMethods.GetRawInputData(lParam);
                if (data.Header.Type == RawInputDeviceType.Keyboard)
                {
                    if ((data.Data.Keyboard.Flags & NativeMethods.RawInputKeyboardFlags.Break) != 0)
                    {
                        _viewModel.ScanCode = data.Data.Keyboard.MakeCode;
                        _viewModel.Key = (VirtualKey)data.Data.Keyboard.VirtualKey;
                        _viewModel.ExtraInformation = data.Data.Keyboard.ExtraInformation;
                    }
                }
                else if (data.Header.Type == RawInputDeviceType.Hid)
                {
                }
                handled = true;
            }
            return default;
        }
    }
}
