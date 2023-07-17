using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features.MonitorFeatures;

namespace Exo.Devices.Lg.Monitors;

[ProductId(VendorIdSource.Usb, 0x043E, 0x9A8A)]
public class LgMonitorDriver : HidDriver, IDeviceDriver<IMonitorDeviceFeature>
{
	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	};

	public static async Task<LgMonitorDriver> CreateAsync(string deviceName, CancellationToken cancellationToken)
	{
		// By retrieving the containerId, we'll be able to get all HID devices interfaces of the physical device at once.
		var containerId = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceInterface, deviceName, Properties.System.Devices.ContainerId, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// The display name of the container can be used as a default value for the device friendly name.
		string friendlyName = await DeviceQuery.GetObjectPropertyAsync(DeviceObjectKind.DeviceContainer, containerId, Properties.System.ItemNameDisplay, cancellationToken).ConfigureAwait(false) ??
			throw new InvalidOperationException();

		// Make a device query to fetch all the matching HID device interfaces at once.
		var deviceInterfaces = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.DeviceInterface,
			RequestedDeviceInterfaceProperties,
			Properties.System.Devices.InterfaceClassGuid == DeviceInterfaceClassGuids.Hid &
				Properties.System.Devices.ContainerId == containerId &
				Properties.System.DeviceInterface.Hid.VendorId == 0x043E,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Expected two HID device interfaces.");
		}

		// Find the top-level device by requesting devices with children.
		// The device tree should be very simple in this case, so we expect this to directly return the top level device. It would not work on more complex scenarios.
		var devices = await DeviceQuery.FindAllAsync
		(
			DeviceObjectKind.Device,
			Array.Empty<Property>(),
			Properties.System.Devices.ContainerId == containerId & Properties.System.Devices.Children.Exists(),
			cancellationToken
		).ConfigureAwait(false);

		if (devices.Length != 3)
		{
			throw new InvalidOperationException("Expected three parent devices.");
		}

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
		string? deviceInterfaceName = null;
		string topLevelDeviceName = devices[0].Id;
		ushort nxpVersion = 0xFFFF;

		// Set the top level device name as the last device name now.
		deviceNames[^1] = topLevelDeviceName;

		for (int i = 0; i < deviceInterfaces.Length; i++)
		{
			var deviceInterface = deviceInterfaces[i];
			deviceNames[i] = deviceInterface.Id;

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsagePage.Key, out ushort usagePage))
			{
				throw new InvalidOperationException($"No HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if ((usagePage & 0xFFFE) != 0xFF00)
			{
				throw new InvalidOperationException($"Unexpected HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId))
			{
				throw new InvalidOperationException($"No HID Usage ID associated with the device interface {deviceInterface.Id}.");
			}

			if (usagePage == 0xFF00 && usageId == 0x01)
			{
				deviceInterfaceName = deviceInterface.Id;

				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.VersionNumber.Key, out ushort version))
				{
					nxpVersion = version;
				}
			}
		}

		if (deviceInterfaceName is null)
		{
			throw new InvalidOperationException($"Could not find device interface with correct HID usages on the device interface {devices[0].Id}.");
		}

		byte sessionId = (byte)Random.Shared.Next(1, 256);
		var transport = await HidI2CTransport.CreateAsync(new HidFullDuplexStream(deviceInterfaceName), sessionId, HidI2CTransport.DefaultDdcDeviceAddress, cancellationToken).ConfigureAwait(false);
		// await transport.SetVcpFeatureAsync(0x62, 0x64, cancellationToken).ConfigureAwait(false); // Test setting the sound.
		(ushort scalerVersion, _, _) = await transport.GetVcpFeatureAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
		var data = ArrayPool<byte>.Shared.Rent(1000);
		var length = await transport.GetCapabilitiesAsync(data, cancellationToken).ConfigureAwait(false);
		var rawCapabilities = data.AsSpan(0, data.AsSpan(0, length).IndexOf((byte)0)).ToArray();
		ArrayPool<byte>.Shared.Return(data);
		if (!MonitorCapabilities.TryParse(rawCapabilities, out var parsedCapabilities))
		{
			throw new InvalidOperationException($@"Could not parse monitor capabilities. Value was: ""{Encoding.ASCII.GetString(rawCapabilities)}"".");
		}
		//var opcodes = new VcpCommandDefinition?[256];
		//foreach (var def in parsedCapabilities.SupportedVcpCommands)
		//{
		//	opcodes[def.VcpCode] = def;
		//}
		//for (int i = 0; i < opcodes.Length; i++)
		//{
		//	var def = opcodes[i];
		//	try
		//	{
		//		var result = await transport.GetVcpFeatureAsync((byte)i, cancellationToken).ConfigureAwait(false);
		//		string? name;
		//		string? category;
		//		if (def is null)
		//		{
		//			((VcpCode)i).TryGetNameAndCategory(out name, out category);
		//		}
		//		else
		//		{
		//			name = def.GetValueOrDefault().Name;
		//			category = def.GetValueOrDefault().Category;
		//		}
		//		Console.WriteLine($"[{(def is null ? "U" : "R")}] [{(result.IsTemporary ? "T" : "P")}] {i:X2} {result.CurrentValue:X4} {result.MaximumValue:X4} [{category ?? "Unknown"}] {name ?? "Unknown"}");
		//	}
		//	catch (Exception ex)
		//	{
		//		if (def is not null)
		//		{
		//			Console.WriteLine($"[R] [-] {i:X2} - {ex.Message}");
		//		}
		//	}
		//}
		return new LgMonitorDriver
		(
			transport,
			nxpVersion,
			scalerVersion,
			0, // TODO: Get the DSC version using the LG special command stuff.
			rawCapabilities,
			parsedCapabilities,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName
		);
	}

	private readonly HidI2CTransport _transport;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private readonly byte[] _rawCapabilities;
	private readonly MonitorCapabilities _parsedCapabilities;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;

	private LgMonitorDriver
	(
		HidI2CTransport transport,
		ushort nxpVersion,
		ushort scalerVersion,
		byte dscVersion,
		byte[] rawCapabilities,
		MonitorCapabilities parsedCapabilities,
		ImmutableArray<string> deviceNames,
		string friendlyName
	) : base(deviceNames, friendlyName, default)
	{
		_transport = transport;
		_nxpVersion = nxpVersion;
		_scalerVersion = scalerVersion;
		_dscVersion = dscVersion;
		_rawCapabilities = rawCapabilities;
		_parsedCapabilities = parsedCapabilities;
		_monitorFeatures = FeatureCollection.Empty<IMonitorDeviceFeature>();
		_allFeatures = FeatureCollection.Empty<IDeviceFeature>();
	}

	// It is mentioned on a few forums/reddit posts that SV, DV, NV would stand for Scaler Version, DSC Version, and NXP version.
	// I'm a bit unsure about the NXP thing, but couldn't find better information for now.
	public SimpleVersion FirmwareNxpVersion => new((byte)(_nxpVersion >>> 8), (byte)_nxpVersion);
	public SimpleVersion FirmwareScalerVersion => new((byte)(_scalerVersion >>> 8), (byte)_scalerVersion);
	public byte FirmwareDisplayStreamCompressionVersion => _dscVersion;
	public ReadOnlySpan<byte> RawCapabilities => _rawCapabilities;
	public MonitorCapabilities Capabilities => _parsedCapabilities;

	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
