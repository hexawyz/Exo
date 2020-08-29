using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
					//if (index > 0) Console.ReadKey(true);
					Console.WriteLine((index == 0 ? "╔" : "╠") + new string('═', 39));
					//Console.WriteLine($"Device Handle: {device.Handle:X16}");
					Console.WriteLine($"║ Device Type: {device.DeviceType}");
					Console.WriteLine($"║ Device Name: {device.DeviceName}");
					try { Console.WriteLine($"║ Device Manufacturer: {device.ManufacturerName}"); }
					catch { Console.WriteLine($"║ Device Manufacturer: <Unknown>"); }
					try { Console.WriteLine($"║ Device Product Name: {device.ProductName}"); }
					catch { Console.WriteLine($"║ Device Product Name: <Unknown>"); }

					if (Regex.Match(device.DeviceName, "VID_(?<VendorId>[0-9A-Fa-f]{4})&PID_(?<ProductId>[0-9A-Fa-f]{4})") is var match and { Success: true })
					{
						ushort vendorId = ushort.Parse(match.Groups["VendorId"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
						ushort productId = ushort.Parse(match.Groups["ProductId"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

						var names = UsbProductNameDatabase.LookupVendorAndProductName(vendorId, productId);

						if (names.VendorName.Length > 0)
						{
							Console.WriteLine($"║ Device Vendor Name: {names.VendorName.ToString()}");
							if (names.ProductName.Length > 0)
							{
								Console.WriteLine($"║ Device Product Name: {names.ProductName.ToString()}");
							}
						}
					}

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

					PhysicalDescriptorSetCollection physicalDescriptorSets;

					try { physicalDescriptorSets = device.GetPhysicalDescriptorSets(); }
					catch { }

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

						var values = device.GetValueCapabilities(reportType);

						for (int i = 0; i < values.Length; i++)
						{
							var value = values[i];
							Console.WriteLine($"║ {(i == 0 ? "╒" : "╞")}═══════ {reportType} Value #{i}");
							Console.WriteLine($"║ │ Report ID: {value.ReportID}");
							Console.WriteLine($"║ │ Collection Index: {value.LinkCollection}");
							PrintUsageAndPage("║ │ Collection", value.LinkUsagePage, value.LinkUsage);
							Console.WriteLine("║ ├───────");
							Console.WriteLine($"║ │ Is Nullable: {value.HasNull}");
							Console.WriteLine($"║ │ Value Length: {value.BitSize} bits");
							Console.WriteLine($"║ │ Report Count: {value.ReportCount}");
							Console.WriteLine($"║ │ Units Exponent: {value.UnitsExp}");
							Console.WriteLine($"║ │ Units: {value.Units}");
							Console.WriteLine($"║ │ Logical Min: {value.LogicalMin}");
							Console.WriteLine($"║ │ Logical Max: {value.LogicalMax}");
							Console.WriteLine($"║ │ Physical Min: {value.PhysicalMin}");
							Console.WriteLine($"║ │ Physical Max: {value.PhysicalMax}");
							Console.WriteLine("║ ├───────");
							if (value.IsRange)
							{
								PrintUsagePage("║ │ Value", value.UsagePage);
								Console.WriteLine($"║ │ Button Usage: {MapToKnownUsage(value.UsagePage, value.Range.UsageMin)} .. {MapToKnownUsage(value.UsagePage, value.Range.UsageMax)}");
								Console.WriteLine($"║ │ Data Index: {value.Range.DataIndexMin} .. {value.Range.DataIndexMax}");
							}
							else
							{
								PrintUsageAndPage("║ │ Value", value.UsagePage, value.NotRange.Usage);
								Console.WriteLine($"║ │ Data Index: {value.NotRange.DataIndex}");
							}
							if (value.IsStringRange)
							{
								Console.WriteLine($@"║ │ String #{value.Range.StringMin} .. #{value.Range.StringMax}: ""{device.GetString(value.Range.StringMin)}"" .. ""{device.GetString(value.Range.StringMax)}""");
							}
							else if (value.NotRange.StringIndex > 0)
							{
								Console.WriteLine($@"║ │ String #{value.NotRange.StringIndex}: ""{device.GetString(value.NotRange.StringIndex)}""");
							}
							if (value.IsDesignatorRange)
							{
								Console.WriteLine($"║ │ Designator Index: {value.Range.DesignatorMin} .. {value.Range.DesignatorMax}");
							}
							else
							{
								Console.WriteLine($"║ │ Designator Index: {value.NotRange.DesignatorIndex}");
							}
							Console.WriteLine($"║ │ Is Absolute: {value.IsAbsolute}");
							Console.WriteLine($"║ │ Is Alias: {value.IsAlias}");
						}

						if (values.Length > 0)
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
