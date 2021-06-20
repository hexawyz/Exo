using DeviceTools.RawInput;
using DeviceTools;
using DeviceTools.DisplayDevices;
using System;
using System.Linq;
using System.Text;
using System.Text.Unicode;
using DeviceTools.DisplayDevices.Mccs;

namespace DeviceTools.Cli
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			bool enumerateMonitors = false;

			if (enumerateMonitors)
			{
				foreach (var adapter in DisplayAdapterDevice.GetAll(false))
				{
					Console.WriteLine($"Adapter Device Name: {adapter.DeviceName}");
					Console.WriteLine($"Adapter Device Description: {adapter.Description}");
					Console.WriteLine($"Adapter Device Id: {adapter.DeviceId}");
					Console.WriteLine($"Adapter Device Key: {adapter.RegistryPath}");
					Console.WriteLine($"Adapter is Attached to Desktop: {adapter.IsAttachedToDesktop}");
					Console.WriteLine($"Adapter is Primary Device: {adapter.IsPrimaryDevice}");
					foreach (var monitor in adapter.GetMonitors(false))
					{
						Console.WriteLine($"Monitor Device Name: {monitor.DeviceName}");
						Console.WriteLine($"Monitor Device Description: {monitor.Description}");
						Console.WriteLine($"Monitor Device Id: {monitor.DeviceId}");
						Console.WriteLine($"Monitor Device Key: {monitor.RegistryPath}");
						Console.WriteLine($"Monitor is Active: {monitor.IsActive}");
						Console.WriteLine($"Monitor is Attached: {monitor.IsAttached}");
					}
				}

				//foreach (string s in Device.EnumerateAllDevices(DeviceClassGuids.Display))
				//{
				//	Console.WriteLine(s);
				//}

				//foreach (string s in Device.EnumerateAllDevices(DeviceClassGuids.Monitor))
				//{
				//	Console.WriteLine(s);
				//}

				//foreach (string s in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.DisplayAdapter))
				//{
				//	Console.WriteLine(s);
				//}

				//foreach (string s in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Monitor))
				//{
				//	Console.WriteLine(s);
				//}

				foreach (var monitor in LogicalMonitor.GetAll())
				{
					Console.WriteLine($"Logical monitor name: {monitor.Name}");

					foreach (var physicalMonitor in monitor.GetPhysicalMonitors())
					{
						Console.WriteLine($"Physical monitor description: {physicalMonitor.Description}");

						var capabilitiesString = physicalMonitor.GetCapabilitiesUtf8String();
						Console.WriteLine($"Physical monitor capabilities: {Encoding.ASCII.GetString(capabilitiesString)}");

						if (MonitorCapabilities.TryParse(capabilitiesString, out var capabilities))
						{
							Console.WriteLine($"Physical monitor type: {capabilities!.Type}");
							Console.WriteLine($"Physical monitor model: {capabilities.Model}");
							Console.WriteLine($"Physical monitor MCCS Version: {capabilities.MccsVersion}");

							Console.WriteLine($"Supported DDC/CI commands: {capabilities.SupportedMonitorCommands.Length}");
							foreach (var ddcCiCommand in capabilities.SupportedMonitorCommands)
							{
								Console.WriteLine($"{(byte)ddcCiCommand:X2} {ddcCiCommand}");
							}

							Console.WriteLine($"Supported VCP commands: {capabilities.SupportedVcpCommands.Length}");
							foreach (var vcpCommand in capabilities.SupportedVcpCommands)
							{
								Console.Write($"Command {vcpCommand.VcpCode:X2}");
								if (vcpCommand.Name is { Length: not 0 })
								{
									Console.Write($" - {vcpCommand.Name}");
								}
								Console.WriteLine();

								try
								{
									var reply = physicalMonitor.GetVcpFeature(vcpCommand.VcpCode);

									Console.WriteLine($"Current Value: {reply.CurrentValue:X2}");
									Console.WriteLine($"Maximum Value: {reply.MaximumValue:X2}");
								}
								catch
								{
									Console.WriteLine("Failed to query the VCP code.");
								}

								foreach (var value in vcpCommand.NonContinuousValues)
								{
									Console.Write($"Value {value.Value:X2}");
									if (value.Name is { Length: not 0 })
									{
										Console.Write($" - {value.Name}");
									}
									Console.WriteLine();
								}
							}
						}
						else
						{
							Console.WriteLine("Failed to parse capabilities.");
						}
					}
				}
			}

			using (var collection = new RawInputDeviceCollection())
			{
				collection.Refresh();
				foreach (var (device, index) in collection.Select((d, i) => (device: d, index: i))) // new[] { rgbFusionDevice1, rgbFusionDevice2 })
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

					var names = UsbProductNameDatabase.LookupVendorAndProductName(device.VendorId, device.ProductId);

					if (names.VendorName.Length > 0)
					{
						Console.WriteLine($"║ Device Vendor Name: {names.VendorName.ToString()}");
						if (names.ProductName.Length > 0)
						{
							Console.WriteLine($"║ Device Product Name: {names.ProductName.ToString()}");
						}
					}

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

					PrintDeviceInfo(device);
				}

				if (collection.Count > 0)
				{
					Console.WriteLine("╚" + new string('═', 39));
				}
			}
		}

		private static void PrintDeviceInfo(HidDevice device)
		{
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
				HidUsagePage.PowerDevice => (HidPowerDeviceUsage)usage,
				HidUsagePage.BatterySystem => (HidBatterySystemUsage)usage,
				_ => usage.ToString("X2")
			};
	}
}
