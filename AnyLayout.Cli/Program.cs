using System;
using System.IO;
using System.Linq;
using System.Text;
using AnyLayout.RawInput;

namespace AnyLayout.Cli
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            using (var collection = new RawInputDeviceCollection())
            {
                collection.Refresh();
                foreach (var (device, index) in collection.Select((d, i) => (device: d, index: i)))
                {
                    Console.WriteLine((index == 0 ? "╔" : "╠") + new string('═', 39));
                    //Console.WriteLine($"Device Handle: {device.Handle:X16}");
                    Console.WriteLine($"║ Device Type: {device.DeviceType}");
                    Console.WriteLine($"║ Device Name: {device.DeviceName}");
                    Console.WriteLine($"║ Device Manufacturer: {device.ManufacturerName}");
                    Console.WriteLine($"║ Device Product Name: {device.ProductName}");

                    //IntPtr preparsedData = IntPtr.Zero;
                    //NativeMethods.HidParsingCaps caps = default;
                    //NativeMethods.HidDiscoveryGetPreparsedData(file, out preparsedData);
                    //NativeMethods.HidParsingGetCaps(preparsedData, out caps);
                    //NativeMethods.HidDiscoveryFreePreparsedData(preparsedData);
                    //PrintUsage(caps.UsagePage, caps.Usage);

                    switch (device)
                    {
                        case RawInputMouseDevice mouse:
                            Console.WriteLine($"║ Mouse Id: {mouse.Id}");
                            Console.WriteLine($"║ Mouse Button Count: {mouse.ButtonCount}");
                            Console.WriteLine($"║ Mouse Sample Rate: {mouse.SampleRate}");
                            Console.WriteLine($"║ Mouse has Horizontal Wheel: {mouse.HasHorizontalWheel}");
                            break;
                        case RawInputKeyboardDevice keyboard:
                            Console.WriteLine($"║ Keyboard Type: {keyboard.KeyboardType}");
                            Console.WriteLine($"║ Keyboard SubType: {keyboard.KeyboardSubType}");
                            Console.WriteLine($"║ Keyboard Key Count: {keyboard.KeyCount}");
                            Console.WriteLine($"║ Keyboard Indicator Count: {keyboard.IndicatorCount}");
                            Console.WriteLine($"║ Keyboard Function Key Count: {keyboard.FunctionKeyCount}");
                            Console.WriteLine($"║ Keyboard Mode: {keyboard.KeyboardMode}");
                            break;
                        case RawInputHidDevice hid:
                            Console.WriteLine($"║ Device Vendor ID: {hid.VendorId:X4}");
                            Console.WriteLine($"║ Device Product ID: {hid.ProductId:X4}");
                            Console.WriteLine($"║ Device Version Number: {hid.VersionNumber:X2}");
                            break;
                    }

                    PrintUsageAndPage("║ Device", device.UsagePage, device.Usage);

                    var nodes = device.GetLinkCollectionNodes();

                    Console.WriteLine($"║ Link Collection Nodes: {nodes.Length}");

                    for (int i = 0; i < nodes.Length; i++)
                    {
                        var node = nodes[i];

                        Console.WriteLine($"║ {(i == 0 ? "╒" : "╞")}═══════ Node #{i}");
                        Console.WriteLine($"║ │ Collection Type: {node.CollectionType}");
                        PrintUsageAndPage("║ │ Node", node.LinkUsagePage, node.LinkUsage);
                        Console.WriteLine($"║ │ Is Alias: {node.IsAlias}");
                        Console.WriteLine($"║ │ Parent: {node.Parent}");
                        if (node.ChildCount != 0)
                        {
                            Console.WriteLine($"║ │ Child Count: {node.ChildCount}");
                            Console.WriteLine($"║ │ First Child: #{node.FirstChild}");
                        }
                        Console.WriteLine($"║ │ Next Sibling: {node.NextSibling}");
                    }

                    if (nodes.Length > 0)
                    {
                        Console.WriteLine("║ ╘═══════");
                    }

                    var physicalDescriptorSets = device.GetPhysicalDescriptorSets();

                    foreach (var reportType in new[] { NativeMethods.HidParsingReportType.Input, NativeMethods.HidParsingReportType.Output, NativeMethods.HidParsingReportType.Feature })
                    {
                        var buttons = device.GetButtonCapabilities(reportType);

                        for (int i = 0; i < buttons.Length; i++)
                        {
                            var button = buttons[i];
                            Console.WriteLine($"║ {(i == 0 ? "╒" : "╞")}═══════ {reportType} Button #{i}");
                            Console.WriteLine($"║ │ Report ID: {button.ReportID}");
                            Console.WriteLine($"║ │ Collection Index: {button.LinkCollection}");
                            PrintUsageAndPage("║ │ Collection", button.LinkUsagePage, button.LinkUsage);
                            Console.WriteLine("║ ├───────");
                            if (button.IsRange)
                            {
                                PrintUsagePage("║ │ Button", button.UsagePage);
                                Console.WriteLine($"║ │ Button Usage: {MapToKnownUsage(button.UsagePage, button.Range.UsageMin)} .. {MapToKnownUsage(button.UsagePage, button.Range.UsageMax)}");
                                Console.WriteLine($"║ │ Data Index: {button.Range.DataIndexMin} .. {button.Range.DataIndexMax}");
                            }
                            else
                            {
                                PrintUsageAndPage("║ │ Button", button.UsagePage, button.NotRange.Usage);
                                Console.WriteLine($"║ │ Data Index: {button.NotRange.DataIndex}");
                            }
                            if (button.IsStringRange)
                            {
                                Console.WriteLine($@"║ │ String #{button.Range.StringMin} .. #{button.Range.StringMax}: ""{device.GetString(button.Range.StringMin)}"" .. ""{device.GetString(button.Range.StringMax)}""");
                            }
                            else if (button.NotRange.StringIndex > 0)
                            {
                                Console.WriteLine($@"║ │ String #{button.NotRange.StringIndex}: ""{device.GetString(button.NotRange.StringIndex)}""");
                            }
                            if (button.IsDesignatorRange)
                            {
                                Console.WriteLine($"║ │ Designator Index: {button.Range.DesignatorMin} .. {button.Range.DesignatorMax}");
                            }
                            else
                            {
                                Console.WriteLine($"║ │ Designator Index: {button.NotRange.DesignatorIndex}");
                            }
                            Console.WriteLine($"║ │ Is Absolute: {button.IsAbsolute}");
                            Console.WriteLine($"║ │ Is Alias: {button.IsAlias}");
                        }

                        if (buttons.Length > 0)
                        {
                            Console.WriteLine("║ ╘═══════");
                        }
                    }
                }

                if (collection.Count > 0)
                {
                    Console.WriteLine("╚" + new string('═', 39));
                }
            }
        }

        private static void PrintUsageAndPage(string prefix, HidUsagePage usagePage, ushort usage)
        {
            PrintUsagePage(prefix, usagePage);
            PrintUsage(prefix, usagePage, usage);
        }

        private static void PrintUsagePage(string prefix, HidUsagePage usagePage) => Console.WriteLine($"{prefix} Usage Page: {usagePage}");

        private static void PrintUsage(string prefix, HidUsagePage usagePage, ushort usage) => Console.WriteLine($@"{prefix} Usage: {MapToKnownUsage(usagePage, usage)}");

        private static object MapToKnownUsage(HidUsagePage usagePage, ushort usage)
            => usagePage switch
            {
                HidUsagePage.GenericDesktop => (HidGenericDesktopUsage)usage,
                HidUsagePage.Simulation => (HidSimulationUsage)usage,
                HidUsagePage.Vr => (HidVrUsage)usage,
                HidUsagePage.Sport => (HidSportUsage)usage,
                HidUsagePage.Game => (HidGameUsage)usage,
                HidUsagePage.Keyboard => (HidKeyboardUsage)usage,
                HidUsagePage.Consumer => (HidConsumerUsage)usage,
                HidUsagePage.Digitizer => (HidDigitizerUsage)usage,
                _ => usage.ToString("X2")
            };
    }
}
