using System;
using System.IO;
using AnyLayout.RawInput;

namespace AnyLayout.Cli
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            foreach (var device in NativeMethods.GetDevices())
            {
                Console.WriteLine(new string('═', 40));
                Console.WriteLine($"Device Handle: {device.Handle:X16}");
                Console.WriteLine($"Device Type: {device.Type}");
                Console.WriteLine($"Device Name: {NativeMethods.GetDeviceName(device.Handle)}");
                using (var file = NativeMethods.CreateFile(NativeMethods.GetDeviceName(device.Handle), 0, FileShare.ReadWrite, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero))
                {
                    Console.WriteLine($"Device Manufacturer: {NativeMethods.GetManufacturerString(file)}");
                    Console.WriteLine($"Device Product Name: {NativeMethods.GetProductString(file)}");

                    IntPtr preparsedData = IntPtr.Zero;
                    NativeMethods.HidParsingCaps caps = default;
                    NativeMethods.HidDiscoveryGetPreparsedData(file, out preparsedData);
                    NativeMethods.HidParsingGetCaps(preparsedData, out caps);
                    NativeMethods.HidDiscoveryFreePreparsedData(preparsedData);
                    PrintUsage(caps.UsagePage, caps.Usage);
                }

                var info = NativeMethods.GetDeviceInfo(device.Handle);

                switch (info.Type)
                {
                    case NativeMethods.RawInputDeviceType.Mouse:
                        Console.WriteLine($"Mouse Id: {info.Mouse.Id}");
                        Console.WriteLine($"Mouse Button Count: {info.Mouse.NumberOfButtons}");
                        Console.WriteLine($"Mouse Sample Rate: {info.Mouse.SampleRate}");
                        Console.WriteLine($"Mouse has Horizontal Wheel: {info.Mouse.HasHorizontalWheel != 0}");
                        break;
                    case NativeMethods.RawInputDeviceType.Keyboard:
                        Console.WriteLine($"Keyboard Type: {info.Keyboard.Type}");
                        Console.WriteLine($"Keyboard SubType: {info.Keyboard.SubType}");
                        Console.WriteLine($"Keyboard Key Count: {info.Keyboard.NumberOfKeysTotal}");
                        Console.WriteLine($"Keyboard Indicator Count: {info.Keyboard.NumberOfIndicators}");
                        Console.WriteLine($"Keyboard Function Key Count: {info.Keyboard.NumberOfFunctionKeys}");
                        Console.WriteLine($"Keyboard Mode: {info.Keyboard.KeyboardMode}");
                        break;
                    case NativeMethods.RawInputDeviceType.Hid:
                        PrintUsage(info.Hid.UsagePage, info.Hid.Usage);
                        break;
                }
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
