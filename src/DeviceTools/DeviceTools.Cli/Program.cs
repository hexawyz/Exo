using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using DeviceTools.FilterExpressions;
using DeviceTools.Firmware;
using DeviceTools.Firmware.Uefi;
using DeviceTools.HumanInterfaceDevices;
using DeviceTools.HumanInterfaceDevices.Usages;
using DeviceTools.RawInput;

namespace DeviceTools.Cli;

internal static class Program
{
	[Flags]
	private enum VcpCodeEnumerationOptions
	{
		None = 0,
		Documented = 1,
		Undocumented = 2,
	}

	private static readonly Dictionary<Guid, string> DeviceInterfaceClassGuidNames =
		typeof(DeviceInterfaceClassGuids).GetFields()
			.Concat(typeof(DeviceInterfaceClassGuids.KernelStreaming).GetFields())
			.Concat(typeof(DeviceInterfaceClassGuids.NetworkDriverInterfaceSpecification).GetFields())
			.Concat(typeof(DeviceInterfaceClassGuids.BluetoothGattServices).GetFields())
			.Concat(typeof(DeviceInterfaceClassGuids.BluetoothGattServiceClasses).GetFields())
			.Select(f => (f.Name, Value: (Guid)f.GetValue(null)!))
			.ToDictionary(t => t.Value, t => t.Name);

	private static readonly Dictionary<Guid, string> DeviceClassGuidNames =
		typeof(DeviceClassGuids).GetFields()
			.Select(f => (f.Name, Value: (Guid)f.GetValue(null)!))
			.ToDictionary(t => t.Value, t => t.Name);

	private static readonly Dictionary<Guid, string> BusTypeGuidNames =
		typeof(DeviceBusTypeGuids).GetFields()
			.Select(f => (f.Name, Value: (Guid)f.GetValue(null)!))
			.ToDictionary(t => t.Value, t => t.Name);

	private static async Task Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;
		bool enumerateMonitors = true;
		var vcpCodeEnumerationOptions = VcpCodeEnumerationOptions.None;

		if (enumerateMonitors)
		{
			PrintDisplayConfiguration();
			await ListMonitorsAsync(vcpCodeEnumerationOptions);

			await PrintDeviceInterfaces(await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Monitor & Properties.System.Devices.InterfaceEnabled == true, default));
			await PrintDeviceInterfaces(await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.DisplayAdapter & Properties.System.Devices.InterfaceEnabled == true, default));
			await PrintDeviceInterfaces(await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.DisplayDeviceArrival & Properties.System.Devices.InterfaceEnabled == true, default));
		}

		PrintSmBiosInfo();

		//return;

		//using (var collection = new RawInputDeviceCollection())
		//{
		//	collection.Refresh();
		//}

		//PrintHidDevices((from dn in Device.EnumerateAllInterfaces(DeviceInterfaceClassGuids.Hid) select HidDevice.FromPath(dn)).ToArray());

		{
			int index = 0;
			//await foreach (var device in DeviceQuery.EnumerateAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid & Properties.System.Devices.InterfaceEnabled == true, default))
			foreach (var deviceInfo in await DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid & Properties.System.Devices.InterfaceEnabled == true, default))
			{
				var device = HidDevice.FromPath(deviceInfo.Id);

				await PrintHidDeviceAsync(device, index++);

				await PrintDeviceInterfaceProperties("║ ", deviceInfo);
			}

			if (index > 0)
			{
				Console.WriteLine("╚=================================================");
			}
		}
	}

	private static async Task PrintDeviceInterfaces(DeviceObjectInformation[] deviceInterfaces)
	{
		int index = 0;
		foreach (var deviceInterfaceInfo in deviceInterfaces)
		{
			Console.WriteLine($"{(index == 0 ? "╔" : "╠")}= Interface {deviceInterfaceInfo.Id}");
			await PrintDeviceInterfaceProperties("║ ", deviceInterfaceInfo);
			index++;
		}

		if (index > 0)
		{
			Console.WriteLine("╚=================================================");
		}
	}

	private static async Task PrintDeviceInterfaceProperties(string indent, DeviceObjectInformation deviceInterfaceInfo)
	{
		foreach (var p in deviceInterfaceInfo.Properties)
		{
			PrintProperty(indent, p);
		}

		if (deviceInterfaceInfo.Properties.TryGetValue(Properties.System.Devices.DeviceInstanceId.Key, out var value) && value is string deviceId)
		{
			Console.WriteLine($"{indent}╒═ Device ============================");
			foreach (var p in await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.Device, deviceId, default))
			{
				PrintProperty($"{indent}│ ", p);
			}
			Console.WriteLine($"{indent}╘=====================================");
		}

		if (deviceInterfaceInfo.Properties.TryGetValue(Properties.System.Devices.ContainerId.Key, out value) && value is Guid containerId)
		{
			Console.WriteLine($"{indent}╒═ Container =========================");
			foreach (var p in await DeviceQuery.GetObjectPropertiesAsync(DeviceObjectKind.DeviceContainer, containerId, default))
			{
				PrintProperty($"{indent}│ ", p);
			}
			Console.WriteLine($"{indent}╘=====================================");
		}
	}

	private static void PrintProperty(string indent, KeyValuePair<PropertyKey, object?> p)
	{
		if (p.Value is string[] list)
		{
			Console.WriteLine(FormattableString.Invariant($"{indent}{p.Key}="));
			Console.WriteLine($"{indent}╒");
			foreach (var item in list)
			{
				Console.WriteLine($"{indent}│ {item}");
			}
			Console.WriteLine($"{indent}╘");
		}
		else if (p.Key == Properties.System.Devices.ClassGuid.Key && p.Value is Guid classGuid && DeviceClassGuidNames.TryGetValue(classGuid, out string? className))
		{
			Console.WriteLine(FormattableString.Invariant($"{indent}{p.Key}={classGuid:B} ({className})"));
		}
		else if (p.Key == Properties.System.Devices.InterfaceClassGuid.Key && p.Value is Guid interfaceClassGuid && DeviceInterfaceClassGuidNames.TryGetValue(interfaceClassGuid, out string? interfaceClassName))
		{
			Console.WriteLine(FormattableString.Invariant($"{indent}{p.Key}={interfaceClassGuid:B} ({interfaceClassName})"));
		}
		else if (p.Key == Properties.System.Devices.BusTypeGuid.Key && p.Value is Guid busTypeGuid && BusTypeGuidNames.TryGetValue(busTypeGuid, out string? busTypeName))
		{
			Console.WriteLine(FormattableString.Invariant($"{indent}{p.Key}={busTypeGuid:B} ({busTypeName})"));
		}
		else
		{
			Console.WriteLine(FormattableString.Invariant($"{indent}{p.Key}={FormatPropertyValue(p.Value)}"));
		}
	}

	private static string? FormatPropertyValue(object? value) =>
		value switch
		{
			byte u8 => u8.ToString("X2", CultureInfo.InvariantCulture),
			sbyte i8 => i8.ToString("X2", CultureInfo.InvariantCulture),
			ushort u16 => u16.ToString("X4", CultureInfo.InvariantCulture),
			short i16 => i16.ToString("X4", CultureInfo.InvariantCulture),
			uint u32 => u32.ToString("X8", CultureInfo.InvariantCulture),
			int i32 => i32.ToString("X8", CultureInfo.InvariantCulture),
			ulong u64 => u64.ToString("X16", CultureInfo.InvariantCulture),
			long i64 => i64.ToString("X16", CultureInfo.InvariantCulture),
			float f => f.ToString(CultureInfo.InvariantCulture),
			double d => d.ToString(CultureInfo.InvariantCulture),
			byte[] b => "0x" + Convert.ToHexString(b),
			string s => s,
			Guid g => g.ToString("B"),
			DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
			StringResource r => r.Value,
			IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
			object o => o.ToString(),
			null => "null"
		};

	private static async Task PrintHidDevicesAsync(HidDevice[] collection)
	{
		foreach (var (device, index) in collection.Select((d, i) => (device: d, index: i)))
		{
			await PrintHidDeviceAsync(device, index);
		}

		if (collection.Length > 0)
		{
			Console.WriteLine("╚=================================================");
		}
	}

	private static async Task PrintHidDeviceAsync(HidDevice device, int index)
	{
		//if (index > 0) Console.ReadKey(true);
		Console.WriteLine((index == 0 ? "╔" : "╠") + "=================================================");
		//Console.WriteLine($"Device Handle: {device.Handle:X16}");
		//Console.WriteLine($"║ Device Type: {device.DeviceType}");
		Console.WriteLine($"║ Device Name: {device.DeviceName}");
		Console.WriteLine($"║ Device Instance ID: {device.InstanceId}");
		Console.WriteLine($"║ Device Container ID: {device.ContainerId}");
		try { Console.WriteLine($"║ Device Container Display Name: {Device.GetDeviceContainerDisplayName(device.ContainerId)}"); }
		catch { Console.WriteLine($"║ Device Container Display Name: <Unknown>"); }
		try { Console.WriteLine($"║ Device Container Primary Category: {Device.GetDeviceContainerPrimaryCategory(device.ContainerId)}"); }
		catch { Console.WriteLine($"║ Device Container Primary Category: <Unknown>"); }
		try { Console.WriteLine($"║ Device Manufacturer: {await device.GetManufacturerNameAsync(default)}"); }
		catch { Console.WriteLine($"║ Device Manufacturer: <Unknown>"); }
		try { Console.WriteLine($"║ Device Product Name: {await device.GetProductNameAsync(default)}"); }
		catch { Console.WriteLine($"║ Device Product Name: <Unknown>"); }
		try { Console.WriteLine($"║ Device Serial Number: {await device.GetSerialNumberAsync(default)}"); }
		catch { Console.WriteLine($"║ Device Serial Number: <Unknown>"); }

		var deviceId = device.DeviceId;

		// TODO: Add a database for BT Vendor IDs (They are publicly available)
		if (deviceId.VendorIdSource == VendorIdSource.Usb)
		{
			var names = UsbProductNameDatabase.LookupVendorAndProductName(deviceId.VendorId, deviceId.ProductId);

			if (names.VendorName.Length > 0)
			{
				Console.WriteLine($"║ Device Vendor Name: {names.VendorName}");
				if (names.ProductName.Length > 0)
				{
					Console.WriteLine($"║ Device Product Name: {names.ProductName}");
				}
			}
		}

		Console.WriteLine($"║ Device Enumerator: {deviceId.Source}");
		Console.WriteLine($"║ Device Vendor ID Source: {deviceId.VendorIdSource}");
		Console.WriteLine($"║ Device Vendor ID: {deviceId.VendorId:X4}");
		Console.WriteLine($"║ Device Product ID: {deviceId.ProductId:X4}");
		if (deviceId.Version != 0xFFFF)
		{
			Console.WriteLine($"║ Device Version Number: {deviceId.Version:X4}");
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
		default:
			break;
		}

		if (device is RawInputDevice rawInptuDevice)
		{
			PrintUsageAndPage("║ Device ", rawInptuDevice.UsagePage, rawInptuDevice.Usage);
		}

		await PrintDeviceInfoAsync(device);
	}

	private static void PrintDisplayConfiguration()
	{
		var configuration = DisplayConfiguration.GetForActivePaths();

		for (int i = 0; i < configuration.Paths.Count; i++)
		{
			Console.WriteLine((i == 0 ? "╔" : "╠") + new string('═', 39));
			var path = configuration.Paths[i];
			var source = path.SourceInfo;
			var target = path.TargetInfo;
			bool sameAdapter = source.Adapter == target.Adapter;

			Console.WriteLine($"║ Path Is Active: {path.IsActive}");
			Console.WriteLine($"║ Path Supports Virtual Mode: {path.SupportsVirtualMode}");
			if (sameAdapter)
			{
				Console.WriteLine($"║ Adapter ID: {source.Adapter.Id:X16}");
				Console.WriteLine($"║ Adapter Name: {source.Adapter.GetDeviceName()}");
			}
			Console.WriteLine("║ ╒ Source ═════════════════════════════");
			if (!sameAdapter)
			{
				Console.WriteLine($"║ │ Adapter ID: {source.Adapter.Id:X16}");
				Console.WriteLine($"║ │ Adapter Name: {source.Adapter.GetDeviceName()}");
			}
			Console.WriteLine($"║ │ ID: {source.Id}");
			Console.WriteLine($"║ │ Is In Use: {source.IsInUse}");
			Console.WriteLine($"║ │ Device Name: {source.GetDeviceName()}");
			if (source.CloneGroupId is int cloneGroupId)
			{
				Console.WriteLine($"║ │ Clone Group ID: {cloneGroupId}");
			}
			if (source.Mode is { } sourceMode)
			{
				Console.WriteLine("║ │ ┌ Mode ─────────────────────────────");
				Console.WriteLine($"║ │ │ Size: {sourceMode.Width}x{sourceMode.Height}");
				Console.WriteLine($"║ │ │ Pixel Format: {sourceMode.PixelFormat}");
				Console.WriteLine("║ │ └───────────────────────────────────");
			}
			Console.WriteLine("║ ╞ Target ═════════════════════════════");
			if (!sameAdapter)
			{
				Console.WriteLine($"║ │ Adapter ID: {target.Adapter.Id:X16}");
				Console.WriteLine($"║ │ Adapter Name: {target.Adapter.GetDeviceName()}");
			}
			Console.WriteLine($"║ │ ID: {target.Id}");
			Console.WriteLine($"║ │ Is Head-Mounted Display: {target.IsHeadMountedDisplay}");
			Console.WriteLine($"║ │ Is In Use: {target.IsInUse}");
			Console.WriteLine($"║ │ Is Available: {target.IsAvailable}");
			Console.WriteLine($"║ │ Is Forcible: {target.IsForcible}");
			Console.WriteLine($"║ │ Is Boot Persistent Forced: {target.IsBootPersistentForced}");
			Console.WriteLine($"║ │ Is Path Persistent Forced: {target.IsPathPersistentForced}");
			Console.WriteLine($"║ │ Is Non Persistent Forced: {target.IsNonPersistentForced}");
			var targetDeviceName = target.GetDeviceNameInformation();
			Console.WriteLine($"║ │ ┌ Device Name ──────────────────────");
			Console.WriteLine($"║ │ │ Name: {targetDeviceName.GetMonitorDeviceName()}");
			Console.WriteLine($"║ │ │ Friendly Name: {targetDeviceName.GetMonitorFriendlyDeviceName()}");
			if (targetDeviceName.ConnectorInstance == 0)
			{
				Console.WriteLine($"║ │ │ Connection: {targetDeviceName.OutputTechnology}");
			}
			else
			{
				Console.WriteLine($"║ │ │ Connection: {targetDeviceName.OutputTechnology} #{targetDeviceName.ConnectorInstance}");
			}
			if (targetDeviceName.IsEdidValid)
			{
				Console.WriteLine($"║ │ │ EDID: {targetDeviceName.EdidVendorId}{targetDeviceName.EdidProductId:X4}");
			}
			Console.WriteLine($"║ │ └───────────────────────────────────");
			Console.WriteLine($"║ │ Output Technology: {target.OutputTechnology}");
			Console.WriteLine($"║ │ Refresh Rate: {target.RefreshRate}");
			Console.WriteLine($"║ │ Scaling: {target.Scaling}");
			Console.WriteLine($"║ │ Rotation: {target.Rotation}");
			if (target.Mode is { } targetMode)
			{
				Console.WriteLine("║ │ ┌ Mode ─────────────────────────────");
				Console.WriteLine($"║ │ │ Actual Size: {targetMode.VideoSignalInfo.ActualSize.Width}x{targetMode.VideoSignalInfo.ActualSize.Height}");
				Console.WriteLine($"║ │ │ TotalSize Size: {targetMode.VideoSignalInfo.TotalSize.Width}x{targetMode.VideoSignalInfo.TotalSize.Height}");
				Console.WriteLine($"║ │ │ Video Standard: {targetMode.VideoSignalInfo.VideoStandard}");
				Console.WriteLine($"║ │ │ Pixel Clock Rate: {targetMode.VideoSignalInfo.PixelClockRate}");
				Console.WriteLine($"║ │ │ Horizontal Frequency: {targetMode.VideoSignalInfo.HorizontalSyncFrequency}");
				Console.WriteLine($"║ │ │ Vertical Frequency: {targetMode.VideoSignalInfo.VerticalSyncFrequency}");
				Console.WriteLine($"║ │ │ Vertical Frequency Divider: {targetMode.VideoSignalInfo.VerticalSyncFrequencyDivider}");
				Console.WriteLine($"║ │ │ Scanline Ordering: {targetMode.VideoSignalInfo.ScanlineOrdering}");
				Console.WriteLine("║ │ └───────────────────────────────────");
			}
			if (target.DesktopMode is { } desktopMode)
			{
				Console.WriteLine("║ │ ┌ Desktop Mode ─────────────────────");
				Console.WriteLine($"║ │ │ Clip: {desktopMode.DesktopImageClip}");
				Console.WriteLine($"║ │ │ Region: {desktopMode.DesktopImageRegion}");
				Console.WriteLine("║ │ └───────────────────────────────────");
			}
			Console.WriteLine("║ ╘");
		}
		if (configuration.Paths.Count > 0)
		{
			Console.WriteLine("╚" + new string('═', 39));
		}
	}

	private static void PrintAdapters()
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
	}

	private static void PrintMonitors(VcpCodeEnumerationOptions vcpCodeEnumerationOptions)
	{
		foreach (var monitor in LogicalMonitor.GetAll())
		{
			Console.WriteLine($"Logical monitor device name: {monitor.GetMonitorInformation().DeviceName}");

			foreach (var physicalMonitor in monitor.GetPhysicalMonitors())
			{
				Console.WriteLine($"Physical monitor description: {physicalMonitor.Description}");

				ReadOnlyMemory<byte> capabilitiesString;
				try
				{
					capabilitiesString = physicalMonitor.GetCapabilitiesUtf8String();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Physical monitor capabilities: <{ex.Message}>");
					continue;
				}

				Console.WriteLine($"Physical monitor capabilities: {Encoding.ASCII.GetString(capabilitiesString.Span)}");
				if (MonitorCapabilities.TryParse(capabilitiesString.Span, out var capabilities))
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
					if ((vcpCodeEnumerationOptions & (VcpCodeEnumerationOptions.Documented | VcpCodeEnumerationOptions.Undocumented)) != 0)
					{
						var enumeratedCodes = new HashSet<byte>();
						foreach (var vcpCommand in capabilities.SupportedVcpCommands)
						{
							enumeratedCodes.Add(vcpCommand.VcpCode);

							if ((vcpCodeEnumerationOptions & (VcpCodeEnumerationOptions.Documented)) != 0)
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

						if ((vcpCodeEnumerationOptions & (VcpCodeEnumerationOptions.Undocumented)) != 0)
						{
							for (int i = 0; i < 256; i++)
							{
								if (enumeratedCodes.Contains((byte)i)) continue;
								Console.Write($"Command {(byte)i:X2}");
								Console.WriteLine();

								try
								{
									var reply = physicalMonitor.GetVcpFeature((byte)i);

									Console.WriteLine($"Current Value: {reply.CurrentValue:X2}");
									Console.WriteLine($"Maximum Value: {reply.MaximumValue:X2}");
								}
								catch
								{
									Console.WriteLine("Failed to query the VCP code.");
								}
							}
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

	private static async Task PrintDeviceInfoAsync(HidDevice device)
	{
		try
		{
			//var nodes = await device.GetLinkCollectionNodesAsync(default);

			//Console.WriteLine($"║ Link Collection Nodes: {nodes.Length}");

			//for (int i = 0; i < nodes.Length; i++)
			//{
			//	var node = nodes[i];

			//	Console.WriteLine($"║ {(i == 0 ? "╒" : "╞")}═══════ Node #{i}");
			//	Console.WriteLine($"║ │ Collection Type: {node.CollectionType}");
			//	PrintUsageAndPage("║ │ Node", node.LinkUsagePage, node.LinkUsage);
			//	Console.WriteLine($"║ │ Is Alias: {node.IsAlias}");
			//	Console.WriteLine($"║ │ Parent: {node.Parent}");
			//	if (node.ChildCount != 0)
			//	{
			//		Console.WriteLine($"║ │ Child Count: {node.ChildCount}");
			//		Console.WriteLine($"║ │ First Child: #{node.FirstChild}");
			//	}
			//	Console.WriteLine($"║ │ Next Sibling: {node.NextSibling}");
			//}

			//if (nodes.Length > 0)
			//{
			//	Console.WriteLine("║ ╘═══════");
			//}

			var collectionDescriptor = await device.GetCollectionDescriptorAsync(default);

			PhysicalDescriptorSetCollection physicalDescriptorSets;

			try { physicalDescriptorSets = device.GetPhysicalDescriptorSets(); }
			catch { }

			bool firstCollection = true;
			foreach (var reportCollection in new HidReportDescriptorCollection[] { collectionDescriptor.InputReports, collectionDescriptor.OutputReports, collectionDescriptor.FeatureReports })
			{
				if (reportCollection.Count == 0) continue;
				Console.WriteLine($"║ {(firstCollection ? "╔" : "╠")}═══════ {reportCollection.ReportType} Reports");
				firstCollection = false;
				Console.WriteLine($"║ ║ Maximum Report Length: {reportCollection.MaximumReportLength}");

				for (int i = 0; i < reportCollection.Count; i++)
				{
					var report = reportCollection[i];
					Console.WriteLine($"║ ║ {(i == 0 ? "╔" : "╠")}═══════ Report #{report.ReportId:X2}");
					Console.WriteLine($"║ ║ ║ Report Length: {report.ReportSize}");

					for (int j = 0; j < report.Channels.Count; j++)
					{
						var channel = report.Channels[j];
						var value = channel as HidValueDescriptor;
						Console.WriteLine($"║ ║ ║ {(j == 0 ? "╒" : "╞")}═══════ Channel #{j}");
						PrintUsageAndPage("║ ║ ║ │ Collection ", channel.LinkCollection.UsagePage, channel.LinkCollection.Usage);
						Console.WriteLine("║ ║ ║ ├───────");
						if (channel.IsRange)
						{
							PrintUsagePage("║ ║ ║ │ ", channel.UsagePage);
							Console.WriteLine($"║ ║ ║ │ Usage: {UsageToString(channel.UsagePage, channel.UsageRange.Minimum)} .. {UsageToString(channel.UsagePage, channel.UsageRange.Maximum)}");
							Console.WriteLine($"║ ║ ║ │ Data Index: {channel.DataIndexRange.Minimum} .. {channel.DataIndexRange.Maximum}");
						}
						else
						{
							PrintUsageAndPage("║ ║ ║ │ ", channel.UsagePage, channel.UsageRange.Minimum);
							Console.WriteLine($"║ ║ ║ │ Data Index: {channel.DataIndexRange.Minimum}");
						}
						if (value is not null)
						{
							Console.WriteLine($"║ ║ ║ │ Is Nullable: {value.HasNullValue}");
						}
						Console.WriteLine($"║ ║ ║ │ Item Size: {channel.ItemBitLength}");
						Console.WriteLine($"║ ║ ║ │ Item Count: {channel.ItemCount}");
						Console.WriteLine($"║ ║ ║ │ Report Start Byte Index: {channel.SequenceByteIndex}");
						Console.WriteLine($"║ ║ ║ │ Report Byte Length: {channel.SequenceByteLength}");
						Console.WriteLine($"║ ║ ║ │ Sequence Bit Offset: {channel.SequenceBitOffset}");
						Console.WriteLine($"║ ║ ║ │ Sequence Bit Length: {channel.SequenceBitLength}");
						Console.WriteLine($"║ ║ ║ │ Units: {channel.Unit}");
						if (channel.LogicalRange.Minimum < channel.LogicalRange.Maximum)
						{
							Console.WriteLine($"║ ║ ║ │ Logical Value: {channel.LogicalRange.Minimum} .. {channel.LogicalRange.Maximum}");
						}
						else if (channel.LogicalRange.Minimum != 0)
						{
							Console.WriteLine($"║ ║ ║ │ Logical Value: {channel.LogicalRange.Minimum}");
						}
						if (value is not null)
						{
							if (value.PhysicalRange.Minimum < value.PhysicalRange.Maximum)
							{
								Console.WriteLine($"║ ║ ║ │ Physical Value: {value.PhysicalRange.Minimum} .. {value.PhysicalRange.Maximum}");
							}
							else if (value.PhysicalRange.Minimum != 0)
							{
								Console.WriteLine($"║ ║ ║ │ Physical Value: {value.PhysicalRange.Minimum}");
							}
						}
						if (channel.IsStringRange)
						{
							Console.WriteLine($@"║ ║ ║ │ String #{channel.StringRange.Minimum} .. #{channel.StringRange.Maximum}: ""{await device.GetStringAsync(channel.StringRange.Minimum, default)}"" .. ""{device.GetStringAsync(channel.StringRange.Maximum, default)}""");
						}
						else if (channel.StringRange.Minimum > 0)
						{
							Console.WriteLine($@"║ ║ ║ │ String #{channel.StringRange.Minimum}: ""{await device.GetStringAsync(channel.StringRange.Minimum, default)}""");
						}
						if (channel.IsDesignatorRange)
						{
							Console.WriteLine($"║ ║ ║ │ Designator Index: {channel.DesignatorRange.Minimum} .. {channel.DesignatorRange.Maximum}");
						}
						else if (channel.DesignatorRange.Minimum != 0)
						{
							Console.WriteLine($"║ ║ ║ │ Designator Index: {channel.DesignatorRange.Minimum}");
						}
						Console.WriteLine($"║ ║ ║ │ Is Absolute: {channel.IsAbsolute}");
						Console.WriteLine($"║ ║ ║ │ Is Alias: {channel.IsAlias}");
						//Console.WriteLine($"║ ║ ║ │ Bit Field: {channel.BitField}");
					}

					if (report.Channels.Count > 0)
					{
						Console.WriteLine("║ ║ ║ ╘═══════");
					}
				}

				if (reportCollection.Count > 0)
				{
					Console.WriteLine("║ ║ ╚═══════");
				}
			}

			if (!firstCollection)
			{
				Console.WriteLine("║ ╚═══════");
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"║ Report Descriptors: <{ex.GetType()}>");
		}
	}

	private static void PrintUsageAndPage(string prefix, HidUsagePage usagePage, ushort usage)
	{
		PrintUsagePage(prefix, usagePage);
		PrintUsage(prefix, usagePage, usage);
	}

	private static void PrintUsagePage(string prefix, HidUsagePage usagePage) => Console.WriteLine($"{prefix}Usage Page: {(Enum.IsDefined(usagePage) ? usagePage.ToString() : ((ushort)usagePage).ToString("X4"))}");

	private static void PrintUsage(string prefix, HidUsagePage usagePage, ushort usage) => Console.WriteLine($@"{prefix}Usage: {UsageToString(usagePage, usage)}");

	private static string UsageToString(HidUsagePage usagePage, ushort usage)
		=> usagePage switch
		{
			HidUsagePage.GenericDesktop => UsageToString<HidGenericDesktopUsage>(usage),
			HidUsagePage.Simulation => UsageToString<HidSimulationUsage>(usage),
			HidUsagePage.VirtualReality => UsageToString<HidVrUsage>(usage),
			HidUsagePage.Sport => UsageToString<HidSportUsage>(usage),
			HidUsagePage.Game => UsageToString<HidGameUsage>(usage),
			HidUsagePage.GenericDevice => UsageToString<HidGenericDeviceUsage>(usage),
			HidUsagePage.Keyboard => UsageToString<HidKeyboardUsage>(usage),
			HidUsagePage.Led => UsageToString<HidLedUsage>(usage),
			HidUsagePage.Telephony => UsageToString<HidTelephonyUsage>(usage),
			HidUsagePage.Consumer => UsageToString<HidConsumerUsage>(usage),
			HidUsagePage.Digitizer => UsageToString<HidDigitizerUsage>(usage),
			HidUsagePage.Haptics => UsageToString<HidHapticUsage>(usage),
			HidUsagePage.EyeAndHeadTrackers => UsageToString<HidEyeAndHeadTrackerUsage>(usage),
			HidUsagePage.AuxiliaryDisplay => UsageToString<HidAuxiliaryDisplayUsage>(usage),
			HidUsagePage.Sensor => UsageToString<HidSensorUsage>(usage),
			HidUsagePage.MedicalInstrument => UsageToString<HidMedicalInstrumentUsage>(usage),
			HidUsagePage.BrailleDisplay => UsageToString<HidBrailleDisplayUsage>(usage),
			HidUsagePage.LightingAndIllumination => UsageToString<HidLightingAndIlluminationUsage>(usage),
			HidUsagePage.CameraControl => UsageToString<HidCameraControlUsage>(usage),
			HidUsagePage.PowerDevice => UsageToString<HidPowerDeviceUsage>(usage),
			HidUsagePage.BatterySystem => UsageToString<HidBatterySystemUsage>(usage),
			HidUsagePage.FidoAlliance => UsageToString<HidFidoAllianceUsage>(usage),
			_ => usage.ToString("X4")
		};

	private static string UsageToString<T>(ushort usage)
		where T : struct, Enum
	{
		var value = Unsafe.As<ushort, T>(ref usage);

		return Enum.IsDefined(value) ? value.ToString() : usage.ToString("X4");
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	private static async Task ListMonitorsAsync(VcpCodeEnumerationOptions vcpCodeEnumerationOptions)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
	{
		PrintAdapters();

		PrintMonitors(vcpCodeEnumerationOptions);
	}

	private static void ListAllDeviceInterfaces(bool? knownGuids = null)
	{
		var devices = DeviceQuery.FindAllAsync(DeviceObjectKind.DeviceInterface, default).GetAwaiter().GetResult();

		var guids = DeviceInterfaceClassGuidNames;

		foreach (var g in devices.GroupBy(d => (Guid)d.Properties[Properties.System.Devices.InterfaceClassGuid.Key]!))
		{
			bool isKnown = guids.TryGetValue(g.Key, out string? n);

			if (knownGuids is null || knownGuids.GetValueOrDefault() == isKnown)
			{
				Console.WriteLine(isKnown ? n : g.Key.ToString());
				foreach (var d in g)
				{
					Console.WriteLine($"\t{d.Id}");
				}
			}
		}
	}

	private static void ListEfiVariables()
	{
		foreach (var efiVariable in EfiEnvironment.EnumerateVariableValues())
		{
			Console.WriteLine($"{efiVariable.VendorGuid}: {efiVariable.Name} [{efiVariable.Attributes}]");
		}
	}

	private static void PrintSmBiosInfo()
	{
		var smBios = SmBios.GetForCurrentMachine();

		static void PrintHeader(string name)
		{
			Console.Write("# ");
			Console.WriteLine(name);
		}

		PrintHeader("BIOS");
		PrintInfo("Vendor", smBios.BiosInformation.Vendor);
		PrintInfo("Version", smBios.BiosInformation.BiosVersion);
		PrintInfo("Starting Address Segment", smBios.BiosInformation.BiosStartingAddressSegment, "X4");
		PrintInfo("Release Date", smBios.BiosInformation.BiosReleaseDate, "yyyy-MM-dd");
		PrintSize("ROM Size", smBios.BiosInformation.BiosRomSize);
		PrintInfo("Characteristics", smBios.BiosInformation.BiosCharacteristics);
		PrintInfo("Vendor Characteristics", smBios.BiosInformation.VendorBiosCharacteristics, "X8");
		PrintInfo("Extended Characteristics", smBios.BiosInformation.ExtendedBiosCharacteristics);
		PrintInfo("System BIOS Major Release", smBios.BiosInformation.SystemBiosMajorRelease);
		PrintInfo("System BIOS Minor Release", smBios.BiosInformation.SystemBiosMinorRelease);
		PrintInfo("Embedded Controller Firmware Major Release", smBios.BiosInformation.EmbeddedFirmwareControllerMajorRelease);
		PrintInfo("Embedded Controller Firmware Minor Release", smBios.BiosInformation.EmbeddedFirmwareControllerMinorRelease);

		Console.WriteLine();

		PrintHeader("System");
		PrintInfo("Manufacturer", smBios.SystemInformation.Manufacturer);
		PrintInfo("Product Name", smBios.SystemInformation.ProductName);
		PrintInfo("Version", smBios.SystemInformation.Version);
		PrintInfo("Serial Number", smBios.SystemInformation.SerialNumber);
		PrintInfo("UUID", smBios.SystemInformation.Uuid);
		PrintInfo("Wake-up Type", smBios.SystemInformation.WakeUpType);
		PrintInfo("SKU Number", smBios.SystemInformation.SkuNumber);
		PrintInfo("Family", smBios.SystemInformation.Family);

		Console.WriteLine();

		for (int i = 0; i < smBios.ProcessorInformations.Length; i++)
		{
			var p = smBios.ProcessorInformations[i];

			PrintHeader($"Processor {i}");
			PrintInfo("Socket Designation", p.SocketDesignation);
			PrintInfo("Manufacturer", p.ProcessorManufacturer);
			PrintInfo("Version", p.ProcessorVersion);
			PrintInfo("Serial Number", p.SerialNumber);
			PrintInfo("Asset Tag", p.AssetTag);
			PrintInfo("Part Number", p.PartNumber);

			PrintInfo("Part Number", p.PartNumber);

			Console.WriteLine();
		}

		for (int i = 0; i < smBios.MemoryDevices.Length; i++)
		{
			var md = smBios.MemoryDevices[i];

			PrintHeader($"Memory Device {i}");
			PrintInfo("Device Locator", md.DeviceLocator);
			PrintInfo("Bank Locator", md.BankLocator);
			PrintInfo("Manufacturer", md.Manufacturer);
			PrintInfo("Manufacturer ID", md.ModuleManufacturerId);
			PrintInfo("Part Number", md.PartNumber);
			PrintInfo("Size", md.Size);

			Console.WriteLine();
		}
	}

	private static void PrintRowPrefix(string name)
	{
		Console.Write(name);
		Console.Write(':');
		if (name.Length < 31)
		{
			Console.Write(new string(' ', 31 - name.Length));
		}
	}

	private static void PrintInfo(string name, string? value)
	{
		if (value is not null)
		{
			PrintRowPrefix(name);
			Console.WriteLine(value);
		}
	}

	private static readonly string[] Units = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", };

	private static void PrintSize(string name, ulong? value)
	{
		if (value is not null)
		{
			byte unit = (byte)((uint)BitOperations.TrailingZeroCount(value.GetValueOrDefault()) / 10);
			Console.Write(value.GetValueOrDefault() >>> (unit * 10));
			Console.Write(' ');
			Console.WriteLine(Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Units), unit));
		}
	}

	private static void PrintInfo<T>(string name, T value, string? format = null)
		where T : IFormattable
	{
		if (value is not null)
		{
			PrintRowPrefix(name);
			Console.WriteLine(value.ToString(format, CultureInfo.InvariantCulture));
		}
	}

	private static void PrintInfo<T>(string name, T? value, string? format = null)
		where T : struct, IFormattable
	{
		if (value is not null)
		{
			PrintRowPrefix(name);
			Console.WriteLine(value.GetValueOrDefault().ToString(format, CultureInfo.InvariantCulture));
		}
	}
}
