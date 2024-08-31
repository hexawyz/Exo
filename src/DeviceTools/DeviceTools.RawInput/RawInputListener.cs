using System;

namespace DeviceTools.RawInput
{
    internal abstract class RawInputListener
    {
        private const int WM_INPUT_DEVICE_CHANGE = 0xFE;
        private const int WM_INPUT = 0xFF;

        protected RawInputDeviceCollection Devices { get; } = new RawInputDeviceCollection();

        protected IntPtr ProcessMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam)
        {
            if (message == WM_INPUT)
            {
            }
            else if (message == WM_INPUT_DEVICE_CHANGE)
            {
            }
            return default;
        }
    }
}
