using System;
using System.IO;
using AnyLayout.RawInput;

namespace AnyLayout.Cli
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            foreach (var device in RawInputDevice.GetAllDevices())
            {
                Console.WriteLine(new string('═', 40));
                //Console.WriteLine($"Device Handle: {device.Handle:X16}");
                Console.WriteLine($"Device Type: {device.DeviceType}");
                Console.WriteLine($"Device Name: {device.DeviceName}");
                Console.WriteLine($"Device Manufacturer: {device.ManufacturerName}");
                Console.WriteLine($"Device Product Name: {device.ProductName}");

                //IntPtr preparsedData = IntPtr.Zero;
                //NativeMethods.HidParsingCaps caps = default;
                //NativeMethods.HidDiscoveryGetPreparsedData(file, out preparsedData);
                //NativeMethods.HidParsingGetCaps(preparsedData, out caps);
                //NativeMethods.HidDiscoveryFreePreparsedData(preparsedData);
                //PrintUsage(caps.UsagePage, caps.Usage);

                switch (device)
                {
                    case RawInputMouseDevice mouse:
                        Console.WriteLine($"Mouse Id: {mouse.Id}");
                        Console.WriteLine($"Mouse Button Count: {mouse.ButtonCount}");
                        Console.WriteLine($"Mouse Sample Rate: {mouse.SampleRate}");
                        Console.WriteLine($"Mouse has Horizontal Wheel: {mouse.HasHorizontalWheel}");
                        break;
                    case RawInputKeyboardDevice keyboard:
                        Console.WriteLine($"Keyboard Type: {keyboard.KeyboardType}");
                        Console.WriteLine($"Keyboard SubType: {keyboard.KeyboardSubType}");
                        Console.WriteLine($"Keyboard Key Count: {keyboard.KeyCount}");
                        Console.WriteLine($"Keyboard Indicator Count: {keyboard.IndicatorCount}");
                        Console.WriteLine($"Keyboard Function Key Count: {keyboard.FunctionKeyCount}");
                        Console.WriteLine($"Keyboard Mode: {keyboard.KeyboardMode}");
                        break;
                    case RawInputHidDevice hid:
                        Console.WriteLine($"Device Vendor ID: {hid.VendorId:X4}");
                        Console.WriteLine($"Device Product ID: {hid.ProductId:X4}");
                        Console.WriteLine($"Device Version Number: {hid.VersionNumber:X2}");
                        break;
                }

                PrintUsage(device.UsagePage, device.Usage);
            }
        }

        private static void PrintUsage(HidUsagePage usagePage, ushort usage)
        {
            Console.WriteLine($"Device Usage Page: {usagePage}");
            Console.WriteLine($@"Device Usage: {usagePage switch
            {
                HidUsagePage.GenericDesktop => (HidGenericDesktopUsage)usage,
                HidUsagePage.Simulation => (HidSimulationUsage)usage,
                HidUsagePage.Vr => (HidVrUsage)usage,
                HidUsagePage.Sport => (HidSportUsage)usage,
                HidUsagePage.Game => (HidGameUsage)usage,
                HidUsagePage.Keyboard => (HidKeyboardUsage)usage,
                HidUsagePage.Consumer => (HidConsumerUsage)usage,
                HidUsagePage.Digitizer => (HidDigitizerUsage)usage,
                _ => usage
            }}");
        }
    }
}
