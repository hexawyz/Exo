using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeviceTools;
using DeviceTools.HumanInterfaceDevices;
using Exo.Features.LightingFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;
using static Exo.Devices.Gigabyte.RgbFusionIT5702Driver;

namespace Exo.Devices.Gigabyte;

[ProductId(VendorIdSource.Usb, 0x048D, 0x5702)]
public sealed class RgbFusionIT5702Driver :
	HidDriver,
	IDeviceDriver<ILightingDeviceFeature>,
	ILightingControllerFeature,
	IPersistentLightingFeature,
	IUnifiedLightingFeature,
	ILightingZoneEffect<DisabledEffect>,
	ILightingZoneEffect<StaticColorEffect>,
	ILightingZoneEffect<ColorPulseEffect>,
	ILightingZoneEffect<ColorFlashEffect>,
	ILightingZoneEffect<ColorDoubleFlashEffect>,
	ILightingZoneEffect<RainbowCycleEffect>,
	ILightingZoneEffect<RainbowWaveEffect>
{
	internal enum Effect : byte
	{
		None = 0,
		Static = 1,
		Pulse = 2,
		Flash = 3,
		ColorCycle = 4,
		Wave = 6,
		Random = 8,
	}

	[StructLayout(LayoutKind.Explicit, Size = 64)]
	internal struct EffectFeatureReport
	{
		[FieldOffset(0)]
		public byte ReportId; // 0xCC
		[FieldOffset(1)]
		public byte CommandId;

		[FieldOffset(2)]
		public EffectData Data;
	}

	[StructLayout(LayoutKind.Explicit, Size = 62)]
	internal struct EffectData
	{
		/// <summary>Specifies which zones the effect should apply to.</summary>
		/// <remarks>
		/// Although RGB Fusion software does ensure to always keep this value synchronized with <see cref="EffectFeatureReport.CommandId"/>, this is not necessary.
		/// A zone can be updated individually without changing the settings of other zones, and effects can be applied to multiple zones at once.
		/// This is even necessary for the wave effect to work across all light zones, as if each zone was a single led in an ARGB strip.
		/// </remarks>
		[FieldOffset(0)]
		public byte LedMask;

		// The hardware effect kind.
		[FieldOffset(9)]
		public Effect Effect;

		// This would usually be set to 100 all the time, as we want to use the specified RGB color as the default value.
		[FieldOffset(10)]
		public byte MaximumBrightness;
		// Almost always set to zero by RGB Fusion, but probably usable in effects such as flash or pulse.
		[FieldOffset(11)]
		public byte MinimumBrightness;

		[FieldOffset(12)]
		public EffectColor Color0;
		// RGB Fusion seems to indicate that this could be used as a color, but I wonder for which effects because I didn't find any example of this.
		// I'd like to try toying with it, though.
		[FieldOffset(16)]
		public EffectColor Color1;

		// Here, although the underlying fields are all the same, each effect have its own interpretation of the fields.
		[FieldOffset(20)]
		public PulseEffectData PulseEffect;
		[FieldOffset(20)]
		public FlashEffectData FlashEffect;
		[FieldOffset(20)]
		public ColorCycleEffectData ColorCycleEffect;
		[FieldOffset(20)]
		public WaveEffectData WaveEffect;
	}

	[StructLayout(LayoutKind.Explicit, Size = 4)]
	internal struct EffectColor
	{
		[FieldOffset(0)]
		public byte Blue;
		[FieldOffset(1)]
		public byte Green;
		[FieldOffset(2)]
		public byte Red;
		// Ignored
		[FieldOffset(3)]
		public byte Alpha;

		[FieldOffset(0)]
		public RgbColor RgbColor;

		public EffectColor(byte blue, byte green, byte red) : this()
		{
			Blue = blue;
			Green = green;
			Red = red;
		}

		[SkipLocalsInit]
		public static implicit operator EffectColor(RgbColor color) => new EffectColor { RgbColor = color, Alpha = 0 };
	}

	[StructLayout(LayoutKind.Explicit, Size = 12)]
	internal struct PulseEffectData
	{
		[FieldOffset(0)]
		public ushort FadeInTicks;
		[FieldOffset(2)]
		public ushort FadeOutTicks;
		[FieldOffset(4)]
		public ushort DurationTicks;
		[FieldOffset(8)]
		public byte ColorCount; // Set to zero for configured color
		[FieldOffset(9)]
		public byte One; // Set to 1
	}

	[StructLayout(LayoutKind.Explicit, Size = 12)]
	internal struct FlashEffectData
	{
		[FieldOffset(0)]
		public ushort FadeInTicks; // Set to 0x64 … Not sure how to use it otherwise
		[FieldOffset(2)]
		public ushort FadeOutTicks; // Set to 0x64 … Not sure how to use it otherwise
		[FieldOffset(4)]
		public ushort DurationTicks;
		[FieldOffset(8)]
		public byte ColorCount; // Set to zero for configured color
		[FieldOffset(9)]
		public byte One; // Set to 1
		[FieldOffset(10)]
		public byte FlashCount; // Acceptable values may depend on the effect Duration ?
	}

	[StructLayout(LayoutKind.Explicit, Size = 12)]
	internal struct ColorCycleEffectData
	{
		[FieldOffset(0)]
		public ushort ColorDurationInTicks;
		[FieldOffset(2)]
		public ushort TransitionDurationInTicks;
		[FieldOffset(8)]
		public byte ColorCount; // Number of colors to include in the cycle: 0 to 7
	}

	[StructLayout(LayoutKind.Explicit, Size = 12)]
	internal struct WaveEffectData
	{
		[FieldOffset(0)]
		public ushort DurationTicks;
		[FieldOffset(8)]
		public byte ColorCount; // Number of colors to include in the cycle: 0 to 7
	}

	[StructLayout(LayoutKind.Sequential, Size = 64)]
	internal struct DeviceInformationResponse
	{
#pragma warning disable IDE0044 // Add readonly modifier
		public byte ReportId;

		// This could be the field indicating if the chip is on motherboard or as part of another product, but unclear.
		public byte Product;
		// Might also be related to multiple devices. Maybe if there are two controllers on the same board, or just a global thing ?
		public byte DeviceIndex;

		public byte LedCount;

		private byte _firmwareVersion0;
		private byte _firmwareVersion1;
		private byte _firmwareVersion2;
		private byte _firmwareVersion3;

		public uint RawFirmwareVersion
		{
			readonly get => BigEndian.ReadUInt32(_firmwareVersion0);
			set => BigEndian.Write(ref _firmwareVersion0, value);
		}

		public Version GetFirmwareVersion() => new(_firmwareVersion0, _firmwareVersion1, _firmwareVersion2, _firmwareVersion3);

		// Unused ?
		private byte _stripControlLength0;
		private byte _stripControlLength1;

		private byte _reserved0;
		private byte _reserved1;

		// Likely to contain "IT5702-GIGABYTE V1.0.10.0", but could be something else.
		private byte _productName00;
		private byte _productName01;
		private byte _productName02;
		private byte _productName03;
		private byte _productName04;
		private byte _productName05;
		private byte _productName06;
		private byte _productName07;
		private byte _productName08;
		private byte _productName09;
		private byte _productName0A;
		private byte _productName0B;
		private byte _productName0C;
		private byte _productName0D;
		private byte _productName0E;
		private byte _productName0F;
		private byte _productName10;
		private byte _productName11;
		private byte _productName12;
		private byte _productName13;
		private byte _productName14;
		private byte _productName15;
		private byte _productName16;
		private byte _productName17;
		private byte _productName18;
		private byte _productName19;
		private byte _productName1A;
		private byte _productName1B;

		public string GetDeviceName()
		{
			var span = MemoryMarshal.CreateReadOnlySpan(ref _productName00, 28);

			if (span.IndexOf((byte)0) is int endIndex and >= 0)
			{
				span = span[..endIndex];
			}

			return Encoding.UTF8.GetString(span);
		}

		// These calibration fields are used to define the order of R,G,B components on the various outputs.
		// The mapping between calibration data and actual led zones might be version dependent.
		public RgbCalibration RgbCalibration0;
		public RgbCalibration RgbCalibration1;
		public RgbCalibration RgbCalibration2;
		public RgbCalibration RgbCalibration3;

		private byte _chipId0;
		private byte _chipId1;
		private byte _chipId2;
		private byte _chipId3;

		// This would include the device ID. e.g. 0x57020100 for IT5702. Maybe also includes some kind of revision ID as the 0x0100 part.
		public uint ChipId
		{
			readonly get => LittleEndian.ReadUInt32(_chipId0);
			set => LittleEndian.Write(ref _chipId0, value);
		}

		private byte _reserved2;
		private byte _reserved3;
		private byte _reserved4;
		private byte _reserved5;
#pragma warning restore IDE0044 // Add readonly modifier
	}

	[StructLayout(LayoutKind.Explicit, Size = 64)]
	internal struct CalibrationFeatureReport
	{
		[FieldOffset(0)]
		public byte ReportId; // 0xCC
		[FieldOffset(1)]
		public byte CommandId;

		[FieldOffset(2)]
		public RgbCalibration Calibration0;
		[FieldOffset(6)]
		public RgbCalibration Calibration1;
		[FieldOffset(10)]
		public RgbCalibration Calibration2;
		[FieldOffset(14)]
		public RgbCalibration Calibration3;
	}

	[StructLayout(LayoutKind.Sequential, Size = 4, Pack = 1)]
	internal struct RgbCalibration
	{
		public byte B;
		public byte G;
		public byte R;
	}

	private static readonly Guid Z490MotherboardUnifiedZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardIoZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardPchZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardPciZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardLed1ZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardLed2ZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardDigitalLed1ZoneId = Guid.Empty;
	private static readonly Guid Z490MotherboardDigitalLed2ZoneId = Guid.Empty;

	private static readonly Guid[] Z490MotherboardGuids = new[]
	{
		Z490MotherboardIoZoneId,
		Z490MotherboardPchZoneId,
		Z490MotherboardPciZoneId,
		Z490MotherboardLed1ZoneId,
		Z490MotherboardLed2ZoneId,
		Z490MotherboardDigitalLed1ZoneId,
		Z490MotherboardDigitalLed2ZoneId,
	};

	private static readonly EffectColor[] DefaultPalette = new[]
	{
		new EffectColor(255, 0, 0),
		new EffectColor(255, 127, 0),
		new EffectColor(255, 255, 0),
		new EffectColor(0, 255, 0),
		new EffectColor(0, 0, 255),
		new EffectColor(75, 0, 130),
		new EffectColor(148, 0, 211),
	};

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
			var (ledCount, productName) = GetDeviceInformation(hidStream);
			return new RgbFusionIT5702Driver
			(
				new HidFullDuplexStream(ledDeviceInterfaceName),
				Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
				productName,
				ledCount
			);
		}
		catch
		{
			await hidStream.DisposeAsync().ConfigureAwait(false);
			throw;
		}
	}

	private static (byte LedCount, string ProductName) GetDeviceInformation(HidFullDuplexStream hidStream)
	{
		Span<byte> message = stackalloc byte[64];

		message[0] = ReportId;
		message[1] = InitializeCommandId;

		hidStream.SendFeatureReport(message);

		message[1..].Clear();

		while (true)
		{
			hidStream.ReceiveFeatureReport(message);

			ref var deviceInformation = ref Unsafe.As<byte, DeviceInformationResponse>(ref message[0]);

			if (deviceInformation.Product == 0x01)
			{
				return (deviceInformation.LedCount, deviceInformation.GetDeviceName());
			}
		}
	}

	private const byte ReportId = 0xCC;

	// All known command IDs to the MCU. Some were reverse-engineered from RGB Fusion.
	private const byte FirstCommandId = 0x20;
	private const byte ExecuteCommandsCommandId = 0x28;
	private const byte SetPaletteCommandId = 0x30;
	private const byte EnableAddressableColorsCommandId = 0x32;
	private const byte UpdateCalibrationCommandId = 0x33;
	private const byte SetAddressableColorLengthCommandId = 0x34;
	private const byte AdressableColors1UpdateCommandId = 0x58;
	private const byte AdressableColors2UpdateCommandId = 0x59;
	private const byte PersistSettingsCommandId = 0x5E;
	private const byte InitializeCommandId = 0x60;

	// State bits indicating what has been updated.
	private const uint StatePendingChangeLedMask = 0x00FF;
	// State bits indicating the addressable light states.
	private const uint StatePendingAddressableMask = 0x0300;
	private const uint StatePendingChangeAddressable = 0x0400;
	// State bit indicating the pending status of unified lighting.
	private const uint StateUnifiedLightingEnabled = 0x0800;
	// State bit indicating if the status of unified lighting has changed. (This may require clearing the command slots)
	private const uint StatePendingChangeUnifiedLighting = 0x1000;

	private readonly HidFullDuplexStream _stream;
	private readonly object _lock = new object();
	private uint _state;
	private readonly EffectColor[] _palette = new EffectColor[7 * 4];
	private readonly byte[] _rgb = new byte[2 * 3 * 32];
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;
	private readonly LightingZone[] _lightingZones;
	private readonly WaveLightingZone _unifiedLightingZone;
	private readonly ReadOnlyCollection<LightingZone> _lightingZoneCollection;

	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	private RgbFusionIT5702Driver(HidFullDuplexStream stream, ImmutableArray<string> deviceNames, string productName, int ledCount)
		: base(deviceNames, productName ?? "RGB Fusion 2.0 Controller", default)
	{
		_stream = stream;

		_lightingFeatures = FeatureCollection.Create<ILightingDeviceFeature, RgbFusionIT5702Driver, ILightingControllerFeature, IUnifiedLightingFeature, IPersistentLightingFeature>(this);
		_allFeatures = FeatureCollection.Create<IDeviceFeature, RgbFusionIT5702Driver, ILightingControllerFeature, IUnifiedLightingFeature, IPersistentLightingFeature>(this);

		_unifiedLightingZone = new WaveLightingZone((byte)((1 << ledCount) - 1), Z490MotherboardUnifiedZoneId, this);
		_lightingZones = new LightingZone[ledCount];
		for (int i = 0; i < _lightingZones.Length; i++)
		{
			byte ledMask = (byte)(1 << i);
			_lightingZones[i] = i < ledCount - 2 ?
				new LightingZone(ledMask, Z490MotherboardGuids[i], this) :
				new AddressableLightingZone(ledMask, Z490MotherboardGuids[i], this);
		}
		_lightingZoneCollection = new(_lightingZones);
		_palette = (EffectColor[])DefaultPalette.Clone();

		// Initialize the state to mark everything as pending updates, so that everything works properly.
		_state = StatePendingChangeLedMask | StatePendingChangeAddressable | StatePendingChangeUnifiedLighting;

		// Test:
		//(_unifiedLightingZone as ILightingZoneEffect<StaticColorEffect>).ApplyEffect(new StaticColorEffect(new(255, 0, 255)));
		//(_unifiedLightingZone as ILightingZoneEffect<ColorPulseEffect>).ApplyEffect(new ColorPulseEffect(new(255, 0, 255)));
		//(_unifiedLightingZone as ILightingZoneEffect<ColorFlashEffect>).ApplyEffect(new ColorFlashEffect(new(255, 0, 255)));
		//(_unifiedLightingZone as ILightingZoneEffect<ColorDoubleFlashEffect>).ApplyEffect(new ColorDoubleFlashEffect(new(255, 0, 255)));
		//(_unifiedLightingZone as ILightingZoneEffect<RainbowCycleEffect>).ApplyEffect(new RainbowCycleEffect());
		//(_unifiedLightingZone as ILightingZoneEffect<RainbowWaveEffect>).ApplyEffect(new RainbowWaveEffect());
		//ApplyChanges();
		//ApplyPaletteColors();
	}

	public override ValueTask DisposeAsync() => _stream.DisposeAsync();

	void ILightingControllerFeature.ApplyChanges() => ApplyChanges();
	void IUnifiedLightingFeature.ApplyChanges() => ApplyChanges();

	private void ApplyChanges()
	{
		Span<byte> buffer = stackalloc byte[64];
		buffer[0] = 0xCC;
		lock (_lock)
		{
			// Only overwrite command slots if some zones have changed.
			if ((_state & StatePendingChangeLedMask) != 0)
			{
				// Use a different strategy depending on whether we use unified lighting or not.
				if ((_state & StateUnifiedLightingEnabled) != 0)
				{
					buffer[1] = FirstCommandId;
					_unifiedLightingZone.GetEffectData(out Unsafe.As<byte, EffectData>(ref buffer[2]));
					_stream.SendFeatureReport(buffer);

					// If needed, clear all the other command slots.
					// We only need to do this the first time we switch to unified lighting mode.
					if ((_state & StatePendingChangeUnifiedLighting) != 0)
					{
						buffer[2..].Clear();
						for (int i = 1; i < _lightingZones.Length; i++)
						{
							buffer[1] = (byte)(FirstCommandId | i & 0x7);
							_stream.SendFeatureReport(buffer);
						}
						// Set the command update bits to the maximum, in order for everything to be taken into account.
						buffer[2] = _unifiedLightingZone.LedMask;
					}
					else
					{
						// Set the command update bits to only the first command.
						buffer[2] = 0x01;
					}
				}
				else
				{
					for (int i = 0; i < _lightingZones.Length; i++)
					{
						var lightingZone = _lightingZones[i];
						// Only update the led zones that have changed.
						if ((_state & lightingZone.LedMask) != 0)
						{
							buffer[1] = (byte)(FirstCommandId | i & 0x7);
							lightingZone.GetEffectData(out Unsafe.As<byte, EffectData>(ref buffer[2]));
							_stream.SendFeatureReport(buffer);
						}
					}
					// Set the command update bits to the updated commands. (So they don't even need to be reread)
					buffer[2] = (byte)(_state & StatePendingChangeLedMask);
				}
				// Flush the commands. (The mask has been set previously)
				buffer[1] = ExecuteCommandsCommandId;
				buffer[3..].Clear();
				_stream.SendFeatureReport(buffer);
			}

			if ((_state & StatePendingChangeAddressable) != 0)
			{
				buffer[1] = EnableAddressableColorsCommandId;
				buffer[2] = (byte)((_state & StatePendingAddressableMask) >> 8);
				buffer[3..].Clear();
				_stream.SendFeatureReport(buffer);
			}

			// Clear all the status bits that we consumed during this update.
			_state &= ~(StatePendingChangeLedMask | StatePendingChangeAddressable | StatePendingChangeUnifiedLighting);
		}
	}

	public void ApplyPaletteColors()
	{
		Span<byte> buffer = stackalloc byte[64];
		buffer[0] = 0xCC;
		buffer[1] = SetPaletteCommandId;
		MemoryMarshal.AsBytes(_palette.AsSpan()).CopyTo(buffer[2..]);

		lock (_lock)
		{
			_stream.SendFeatureReport(buffer);
		}
	}

	IReadOnlyCollection<ILightingZone> ILightingControllerFeature.LightingZones => _lightingZoneCollection;

	// TODO: Determine if it should apply settings first.
	public void PersistCurrentConfiguration()
	{
		lock (_lock)
		{
			PersistCurrentConfigurationInternal(_stream);
		}
	}

	private static void PersistCurrentConfigurationInternal(HidFullDuplexStream stream)
	{
		Span<byte> buffer = stackalloc byte[64];

		buffer[0] = ReportId;
		buffer[1] = PersistSettingsCommandId;

		stream.SendFeatureReport(buffer);
	}

	bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => _unifiedLightingZone.GetCurrentEffect() is not NotApplicableEffect;

	Guid ILightingZone.ZoneId => Z490MotherboardUnifiedZoneId;
	ILightingEffect ILightingZone.GetCurrentEffect() => _unifiedLightingZone.GetCurrentEffect();

	void ILightingZoneEffect<DisabledEffect>.ApplyEffect(DisabledEffect effect) => ((ILightingZoneEffect<DisabledEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(StaticColorEffect effect) => ((ILightingZoneEffect<StaticColorEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(ColorPulseEffect effect) => ((ILightingZoneEffect<ColorPulseEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<ColorFlashEffect>.ApplyEffect(ColorFlashEffect effect) => ((ILightingZoneEffect<ColorFlashEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<ColorDoubleFlashEffect>.ApplyEffect(ColorDoubleFlashEffect effect) => ((ILightingZoneEffect<ColorDoubleFlashEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<RainbowCycleEffect>.ApplyEffect(RainbowCycleEffect effect) => ((ILightingZoneEffect<RainbowCycleEffect>)_unifiedLightingZone).ApplyEffect(effect);
	void ILightingZoneEffect<RainbowWaveEffect>.ApplyEffect(RainbowWaveEffect effect) => ((ILightingZoneEffect<RainbowWaveEffect>)_unifiedLightingZone).ApplyEffect(effect);

	bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => ((ILightingZoneEffect<DisabledEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => ((ILightingZoneEffect<StaticColorEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => ((ILightingZoneEffect<ColorPulseEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<ColorFlashEffect>.TryGetCurrentEffect(out ColorFlashEffect effect) => ((ILightingZoneEffect<ColorFlashEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<ColorDoubleFlashEffect>.TryGetCurrentEffect(out ColorDoubleFlashEffect effect) => ((ILightingZoneEffect<ColorDoubleFlashEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<RainbowCycleEffect>.TryGetCurrentEffect(out RainbowCycleEffect effect) => ((ILightingZoneEffect<RainbowCycleEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);
	bool ILightingZoneEffect<RainbowWaveEffect>.TryGetCurrentEffect(out RainbowWaveEffect effect) => ((ILightingZoneEffect<RainbowWaveEffect>)_unifiedLightingZone).TryGetCurrentEffect(out effect);

	private class LightingZone
		: ILightingZone,
		ILightingZoneEffect<DisabledEffect>,
		ILightingZoneEffect<StaticColorEffect>,
		ILightingZoneEffect<ColorPulseEffect>,
		ILightingZoneEffect<ColorFlashEffect>,
		ILightingZoneEffect<ColorDoubleFlashEffect>,
		ILightingZoneEffect<RainbowCycleEffect>
	{
		internal byte LedMask { get; }
		public Guid ZoneId { get; }
		protected ILightingEffect CurrentEffect;
		protected readonly RgbFusionIT5702Driver Owner;
		protected EffectData EffectData;

		public LightingZone(byte ledMask, Guid zoneId, RgbFusionIT5702Driver owner)
		{
			LedMask = ledMask;
			ZoneId = zoneId;
			// Initialize the current effect as disabled, individual lighting.
			// We can't recover the actual configuration from the chip, so applying changes immediately will shut down all lighting.
			EffectData.LedMask = ledMask;
			if (BitOperations.PopCount(ledMask) > 1)
			{
				CurrentEffect = NotApplicableEffect.SharedInstance;
			}
			else
			{
				CurrentEffect = DisabledEffect.SharedInstance;
				EffectData.Effect = Effect.Static;
			}
			Owner = owner;
		}

		public ILightingEffect GetCurrentEffect() => CurrentEffect;

		internal void GetEffectData(out EffectData effectData) => effectData = EffectData;

		// Do the necessary changes to enable or disable unified lighting depending on the situation.
		// Must be called from within the lock.
		protected void ToggleUnifiedLighting()
		{
			bool isUnifyingLightingEnabled = (Owner._state & StateUnifiedLightingEnabled) != 0;

			if (this == Owner._unifiedLightingZone)
			{
				if (!isUnifyingLightingEnabled)
				{
					foreach (var zone in Owner._lightingZones)
					{
						zone.CurrentEffect = NotApplicableEffect.SharedInstance;
						zone.EffectData = new();
					}

					Owner._state |= StateUnifiedLightingEnabled | StatePendingChangeUnifiedLighting;
				}
			}
			else if (isUnifyingLightingEnabled)
			{
				var effect = Owner._unifiedLightingZone.CurrentEffect;
				ref var effectData = ref Owner._unifiedLightingZone.EffectData;

				foreach (var zone in Owner._lightingZones)
				{
					zone.CurrentEffect = effect;
					zone.EffectData = effectData;
				}

				Owner._unifiedLightingZone.CurrentEffect = NotApplicableEffect.SharedInstance;
				Owner._unifiedLightingZone.EffectData = new();

				Owner._state = Owner._state & ~StateUnifiedLightingEnabled | StatePendingChangeUnifiedLighting;
			}
		}

		void ILightingZoneEffect<DisabledEffect>.ApplyEffect(DisabledEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Static,
				};
				CurrentEffect = DisabledEffect.SharedInstance;
			}
		}

		void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(StaticColorEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Static,
					MaximumBrightness = 100,
					Color0 = effect.Color,
				};
				CurrentEffect = effect;
			}
		}

		void ILightingZoneEffect<ColorPulseEffect>.ApplyEffect(ColorPulseEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Pulse,
					MaximumBrightness = 100,
					Color0 = effect.Color,
					PulseEffect =
					{
						FadeInTicks = 1000,
						FadeOutTicks = 1000,
						DurationTicks = 500,
						One = 1,
					}
				};
				CurrentEffect = effect;
			}
		}

		void ILightingZoneEffect<ColorFlashEffect>.ApplyEffect(ColorFlashEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Pulse,
					MaximumBrightness = 100,
					Color0 = effect.Color,
					FlashEffect =
					{
						FadeInTicks = 100,
						FadeOutTicks = 100,
						DurationTicks = 1800,
						One = 1,
						FlashCount = 1,
					}
				};
				CurrentEffect = effect;
			}
		}

		void ILightingZoneEffect<ColorDoubleFlashEffect>.ApplyEffect(ColorDoubleFlashEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Flash,
					MaximumBrightness = 100,
					Color0 = effect.Color,
					FlashEffect =
					{
						FadeInTicks = 100,
						FadeOutTicks = 100,
						DurationTicks = 2000,
						One = 1,
						FlashCount = 2,
					}
				};
				CurrentEffect = effect;
			}
		}

		void ILightingZoneEffect<RainbowCycleEffect>.ApplyEffect(RainbowCycleEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.ColorCycle,
					MaximumBrightness = 100,
					ColorCycleEffect =
					{
						ColorDurationInTicks = 760,
						TransitionDurationInTicks = 660,
						ColorCount = 7,
					}
				};
				CurrentEffect = effect;
			}
		}

		bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorPulseEffect>.TryGetCurrentEffect(out ColorPulseEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorFlashEffect>.TryGetCurrentEffect(out ColorFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<ColorDoubleFlashEffect>.TryGetCurrentEffect(out ColorDoubleFlashEffect effect) => CurrentEffect.TryGetEffect(out effect);
		bool ILightingZoneEffect<RainbowCycleEffect>.TryGetCurrentEffect(out RainbowCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
	}

	private class WaveLightingZone : LightingZone, ILightingZoneEffect<RainbowWaveEffect>
	{
		public WaveLightingZone(byte ledMask, Guid zoneId, RgbFusionIT5702Driver owner) : base(ledMask, zoneId, owner)
		{
		}

		void ILightingZoneEffect<RainbowWaveEffect>.ApplyEffect(RainbowWaveEffect effect)
		{
			lock (Owner._lock)
			{
				ToggleUnifiedLighting();

				EffectData = new EffectData
				{
					LedMask = LedMask,
					Effect = Effect.Wave,
					MaximumBrightness = 100,
					WaveEffect =
					{
						DurationTicks = 570,
						ColorCount = 7,
					}
				};
				CurrentEffect = effect;
			}
		}

		bool ILightingZoneEffect<RainbowWaveEffect>.TryGetCurrentEffect(out RainbowWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
	}

	private class AddressableLightingZone : LightingZone
	{
		public AddressableLightingZone(byte ledMask, Guid zoneId, RgbFusionIT5702Driver owner) : base(ledMask, zoneId, owner)
		{
		}
	}
}
