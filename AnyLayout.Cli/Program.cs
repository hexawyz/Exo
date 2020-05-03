using System;
using System.IO;
using AnyLayout.RawInput;

namespace AnyLayout.Cli
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            using (var collection = new RawInputDeviceCollection())
            {
                collection.Refresh();
                foreach (var device in collection)
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

                    PrintUsage("Device", device.UsagePage, device.Usage);

                    var nodes = device.GetLinkCollectionNodes();

                    Console.WriteLine($"Link Collection Nodes: {nodes.Length}");

                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];

                        Console.WriteLine($"════ Node #{i}");
                        Console.WriteLine($"Collection Type: {node.CollectionType}");
                        PrintUsage("Node", node.LinkUsagePage, node.LinkUsage);
                        Console.WriteLine($"Is Alias: {node.IsAlias}");
                        Console.WriteLine($"Parent: {node.Parent}");
                        if (node.ChildCount != 0)
                        {
                            Console.WriteLine($"Child Count: {node.ChildCount}");
                            Console.WriteLine($"First Child: #{node.FirstChild}");
                        }
                        Console.WriteLine($"Next Sibling: {node.NextSibling}");
                    }
                }
            }
        }

        private static void PrintUsage(string prefix, HidUsagePage usagePage, ushort usage)
        {
            Console.WriteLine($"{prefix} Usage Page: {usagePage}");
            Console.WriteLine($@"{prefix} Usage: {usagePage switch
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
