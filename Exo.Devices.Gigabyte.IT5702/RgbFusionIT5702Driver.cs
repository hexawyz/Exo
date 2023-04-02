using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features.LightingFeatures;
using Exo.Lighting;

namespace Exo.Devices.Gigabyte;

[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct FeatureReport
{
	[FieldOffset(0)]
	public byte ReportId; // 0xCC
	[FieldOffset(1)]
	public FeatureReportHeader Header;

	[FieldOffset(11)]
	public Effect Effect;
	[FieldOffset(12)]
	public ColorEffect ColorEffect;

	[FieldOffset(22)]
	public PulseEffect PulseEffect;
	[FieldOffset(22)]
	public FlashEffect FlashEffect;
	[FieldOffset(22)]
	public ColorCycleEffect ColorCycleEffect;
}

internal struct FeatureReportHeader
{
	public byte CommandId;
	public byte LedMask; // If CommandId between 0x20 and 0x27 included, this is 1 << LedIndex
}

[StructLayout(LayoutKind.Explicit, Size = 10)]
internal struct ColorEffect
{
	[FieldOffset(0)]
	public byte Brightness;
	[FieldOffset(2)]
	public byte Blue;
	[FieldOffset(3)]
	public byte Green;
	[FieldOffset(4)]
	public byte Red;
}

[StructLayout(LayoutKind.Explicit, Size = 10)]
internal struct PulseEffect
{
	[FieldOffset(0)]
	public ushort FadeInTicks;
	[FieldOffset(2)]
	public ushort FadeOutTicks;
	[FieldOffset(4)]
	public ushort DurationTicks;
	[FieldOffset(9)]
	public byte One; // Set to 1
}

[StructLayout(LayoutKind.Explicit, Size = 11)]
internal struct FlashEffect
{
	[FieldOffset(0)]
	public ushort FadeInTicks; // Set to 0x64 … Not sure how to use it otherwise
	[FieldOffset(2)]
	public ushort FadeOutTicks; // Set to 0x64 … Not sure how to use it otherwise
	[FieldOffset(4)]
	public ushort DurationTicks;
	[FieldOffset(9)]
	public byte One; // Set to 1
	[FieldOffset(10)]
	public byte FlashCount; // Acceptable values may depend on the effect Duration ?
}

[StructLayout(LayoutKind.Explicit, Size = 11)]
internal struct ColorCycleEffect
{
	[FieldOffset(0)]
	public ushort ColorDurationInTicks;
	[FieldOffset(2)]
	public ushort TransitionDurationInTicks;
	[FieldOffset(8)]
	public byte ColorCount; // Number of colors to include in the cycle: 0 to 7
}

internal enum Effect : byte
{
	None = 0,
	Static = 1,
	Pulse = 2,
	Flash = 3,
	ColorCycle = 4,
}

[ProductId(VendorIdSource.Usb, 0x048D, 0x5702)]
public sealed class RgbFusionIT5702Driver : HidDriver, IDeviceDriver<ILightingDeviceFeature>, ILightingControllerFeature
{
	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
	};

	public static async Task<RgbFusionIT5702Driver> CreateAsync(string deviceName, CancellationToken cancellationToken)
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
				Properties.System.DeviceInterface.Hid.VendorId == 0x048D,
			cancellationToken
		).ConfigureAwait(false);

		if (deviceInterfaces.Length != 2)
		{
			throw new InvalidOperationException("Expected only two device interfaces.");
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

		if (devices.Length != 1)
		{
			throw new InvalidOperationException("Expected only one parent device.");
		}

		string[] deviceNames = new string[deviceInterfaces.Length + 1];
		string? ledDeviceInterfaceName = null;
		string topLevelDeviceName = devices[0].Id;

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

			if (usagePage != 0xFF89)
			{
				throw new InvalidOperationException($"Unexpected HID Usage Page associated with the device interface {deviceInterface.Id}.");
			}

			if (!deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.UsageId.Key, out ushort usageId))
			{
				throw new InvalidOperationException($"No HID Usage ID associated with the device interface {deviceInterface.Id}.");
			}

			if (usageId == 0xCC)
			{
				ledDeviceInterfaceName = deviceInterface.Id;
			}
		}

		if (ledDeviceInterfaceName is null)
		{
			throw new InvalidOperationException($"Could not find device interface with HID Usage ID 0xCC on the device interface {devices[0].Id}.");
		}

		var hidStream = new HidFullDuplexStream(ledDeviceInterfaceName);
		try
		{
			var (ledCount, name) = GetDeviceInformation(hidStream);
			return new RgbFusionIT5702Driver
			(
				new HidFullDuplexStream(ledDeviceInterfaceName),
				Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
				ledCount
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private static (byte LedCount, string Name) GetDeviceInformation(HidFullDuplexStream hidStream)
	{
		Span<byte> message = stackalloc byte[64];

		message[0] = 0xCC;
		message[1] = 0x60;

		hidStream.SendFeatureReport(message);

		message[1..].Clear();

		while (true)
		{
			hidStream.ReceiveFeatureReport(message);

			if (message[1] == 0x01)
			{
				byte ledCount = message[3];
				// Don't know for sure how long the name can be, but at least 25 characters. We'll stop at the first null byte.
				var name = message.Slice(12, 29);
				if (name.IndexOf((byte)0) is int length and >= 0)
				{
					name = name.Slice(0, length);
				}
				return (ledCount, Encoding.UTF8.GetString(name));
			}
		}
	}

	private readonly HidFullDuplexStream _stream;
	private readonly object _lock = new object();
	private int _changedLeds;
	private readonly FeatureReport[] _zoneSettings;
	private readonly byte[] _rgb = new byte[2 * 3 * 32];
	private readonly byte[] _commonFeatureBuffer = new byte[64];
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	private RgbFusionIT5702Driver(HidFullDuplexStream stream, ImmutableArray<string> deviceNames, int ledCount)
		: base(deviceNames, "RGB Fusion IT5702", default)
	{
		_stream = stream;
		_zoneSettings = new FeatureReport[ledCount];
		for (int i = 0; i < _zoneSettings.Length; i++)
		{
			ref var settings = ref _zoneSettings[i];

			settings.ReportId = 0xCC;
			settings.Header.CommandId = (byte)(0x20 + i);
			settings.Header.LedMask = (byte)(1 << i);
		}

		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, ILightingControllerFeature>(this);
		_allFeatures = FeatureCollection.Create<IDeviceFeature, ILightingControllerFeature>(this);
	}

	public override ValueTask DisposeAsync() => _stream.DisposeAsync();

	void ILightingControllerFeature.ApplyChanges()
	{
		lock (_lock)
		{
			var zoneSettings = _zoneSettings.AsSpan();
			for (int i = 0; i < zoneSettings.Length; i++)
			{
				if ((_changedLeds & 1 << i) != 0)
				{
					_stream.SendFeatureReport(MemoryMarshal.AsBytes(zoneSettings.Slice(i, 1)));
				}
			}
			if (_changedLeds != 0)
			{
				Array.Clear(_commonFeatureBuffer, 0, _commonFeatureBuffer.Length);
				_commonFeatureBuffer[0] = 0xCC;
				_commonFeatureBuffer[1] = 0x01;
				_commonFeatureBuffer[2] = 0xFF;
				_stream.SendFeatureReport(_commonFeatureBuffer);
			}
		}
	}

	IReadOnlyCollection<ILightZone> ILightingControllerFeature.GetLightZones() => throw new NotImplementedException();
}
