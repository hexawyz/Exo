using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Lg.Monitors.LightingEffects;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Features.MonitorFeatures;
using Exo.Lighting;
using Exo.Lighting.Effects;

namespace Exo.Devices.Lg.Monitors;

[ProductId(VendorIdSource.Usb, 0x043E, 0x9A8A)]
public class LgMonitorDriver :
	HidDriver,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<ILgMonitorDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IRawVcpFeature,
	IBrightnessFeature,
	IContrastFeature,
	IMonitorCapabilitiesFeature,
	IMonitorRawCapabilitiesFeature,
	ILgMonitorScalerVersionFeature,
	ILgMonitorNxpVersionFeature,
	ILgMonitorDisplayStreamCompressionVersionFeature,
	IUnifiedLightingFeature,
	ILightingZoneEffect<DisabledEffect>,
	ILightingZoneEffect<StaticColorPreset1Effect>,
	ILightingZoneEffect<StaticColorPreset2Effect>,
	ILightingZoneEffect<StaticColorPreset3Effect>,
	ILightingZoneEffect<StaticColorPreset4Effect>,
	ILightingZoneEffect<ColorCycleEffect>,
	ILightingZoneEffect<ColorWaveEffect>
{
	private static readonly Guid LightingZoneGuid = new(0x7105A4FA, 0x2235, 0x49FC, 0xA7, 0x5A, 0xFD, 0x0D, 0xEC, 0x13, 0x51, 0x99);

	private static readonly Property[] RequestedDeviceInterfaceProperties = new Property[]
	{
		Properties.System.Devices.DeviceInstanceId,
		Properties.System.DeviceInterface.Hid.UsagePage,
		Properties.System.DeviceInterface.Hid.UsageId,
		Properties.System.DeviceInterface.Hid.VersionNumber,
	};

	public static async Task<LgMonitorDriver> CreateAsync(string deviceName, ushort productId, CancellationToken cancellationToken)
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
		string? i2cDeviceInterfaceName = null;
		string? lightingDeviceInterfaceName = null;
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
				i2cDeviceInterfaceName = deviceInterface.Id;

				if (deviceInterface.Properties.TryGetValue(Properties.System.DeviceInterface.Hid.VersionNumber.Key, out ushort version))
				{
					nxpVersion = version;
				}
			}
			else if (usagePage == 0xFF01 && usageId == 0x01)
			{
				lightingDeviceInterfaceName = deviceInterface.Id;
			}
		}

		if (i2cDeviceInterfaceName is null || lightingDeviceInterfaceName is null)
		{
			throw new InvalidOperationException($"Could not find device interfaces with correct HID usages on the device interface {devices[0].Id}.");
		}

		byte sessionId = (byte)Random.Shared.Next(1, 256);
		var i2cTransport = await HidI2CTransport.CreateAsync(new HidFullDuplexStream(i2cDeviceInterfaceName), sessionId, HidI2CTransport.DefaultDdcDeviceAddress, cancellationToken).ConfigureAwait(false);
		(ushort scalerVersion, _, _) = await i2cTransport.GetVcpFeatureAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
		var data = ArrayPool<byte>.Shared.Rent(1000);
		// This special call will return various data, including a byte representing the DSC firmware version. (In BCD format)
		await i2cTransport.SendLgCustomCommandAsync(0xC9, 0x06, data.AsMemory(0, 9), cancellationToken).ConfigureAwait(false);
		byte dscVersion = data[1];
		// This special call will return the monitor model name. We can then use it as the friendly name.
		await i2cTransport.SendLgCustomCommandAsync(0xCA, 0x00, data.AsMemory(0, 10), cancellationToken).ConfigureAwait(false);
		friendlyName = "LG " + Encoding.ASCII.GetString(data.AsSpan(0, data.AsSpan(0, 10).IndexOf((byte)0)));
		var length = await i2cTransport.GetCapabilitiesAsync(data, cancellationToken).ConfigureAwait(false);
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
			i2cTransport,
			new UltraGearLightingTransport(new HidFullDuplexStream(lightingDeviceInterfaceName)),
			nxpVersion,
			scalerVersion,
			dscVersion,
			rawCapabilities,
			parsedCapabilities,
			Unsafe.As<string[], ImmutableArray<string>>(ref deviceNames),
			friendlyName,
			// TODO: Try reading the serial number (I believe it is possible?)
			new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{productId:X4}", null)
		);
	}

	private readonly HidI2CTransport _i2cTransport;
	private readonly UltraGearLightingTransport _lightingTransport;
	private ILightingEffect _currentEffect;
	private bool _currentEffectChanged;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private readonly byte[] _rawCapabilities;
	private readonly MonitorCapabilities _parsedCapabilities;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureCollection<ILgMonitorDeviceFeature> _lgMonitorFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private LgMonitorDriver
	(
		HidI2CTransport transport,
		UltraGearLightingTransport lightingTransport,
		ushort nxpVersion,
		ushort scalerVersion,
		byte dscVersion,
		byte[] rawCapabilities,
		MonitorCapabilities parsedCapabilities,
		ImmutableArray<string> deviceNames,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(deviceNames, friendlyName, configurationKey)
	{
		_i2cTransport = transport;
		_lightingTransport = lightingTransport;
		_currentEffect = DisabledEffect.SharedInstance;
		_nxpVersion = nxpVersion;
		_scalerVersion = scalerVersion;
		_dscVersion = dscVersion;
		_rawCapabilities = rawCapabilities;
		_parsedCapabilities = parsedCapabilities;
		_monitorFeatures = FeatureCollection.Create<
			IMonitorDeviceFeature,
			LgMonitorDriver,
			IMonitorRawCapabilitiesFeature,
			IMonitorCapabilitiesFeature,
			IRawVcpFeature,
			IBrightnessFeature,
			IContrastFeature>(this);
		_lightingFeatures = FeatureCollection.Create<
			ILightingDeviceFeature,
			IUnifiedLightingFeature>(this);
		_lgMonitorFeatures = FeatureCollection.Create<
			ILgMonitorDeviceFeature,
			LgMonitorDriver,
			ILgMonitorScalerVersionFeature,
			ILgMonitorNxpVersionFeature,
			ILgMonitorDisplayStreamCompressionVersionFeature>(this);
		_allFeatures = FeatureCollection.CreateMerged(_lightingFeatures, _monitorFeatures, _lgMonitorFeatures);
	}

	// The firmware archive explicitly names the SV, DV and NV as "Scaler", "DSC" and "NXP"
	// DSC versions are expanded in a single byte, seemingly in BCD format. So, 0x44 for version 44, and 0x51 for version 51.
	public SimpleVersion FirmwareNxpVersion => new((byte)(_nxpVersion >>> 8), (byte)_nxpVersion);
	public SimpleVersion FirmwareScalerVersion => new((byte)(_scalerVersion >>> 8), (byte)_scalerVersion);
	public SimpleVersion FirmwareDisplayStreamCompressionVersion => new((byte)((_dscVersion >>> 4) * 10 | _dscVersion & 0x0F), 0);
	public ReadOnlySpan<byte> RawCapabilities => _rawCapabilities;
	public MonitorCapabilities Capabilities => _parsedCapabilities;

	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	IDeviceFeatureCollection<ILgMonitorDeviceFeature> IDeviceDriver<ILgMonitorDeviceFeature>.Features => _lgMonitorFeatures;
	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => _lightingFeatures;
	public override IDeviceFeatureCollection<IDeviceFeature> Features => _allFeatures;

	public override async ValueTask DisposeAsync()
	{
		await _i2cTransport.DisposeAsync().ConfigureAwait(false);
		await _lightingTransport.DisposeAsync().ConfigureAwait(false);
	}

	public ValueTask SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken) => new(_i2cTransport.SetVcpFeatureAsync(vcpCode, value, cancellationToken));

	public async ValueTask<VcpFeatureReply> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		var result = await _i2cTransport.GetVcpFeatureAsync(vcpCode, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, result.MaximumValue, result.IsTemporary);
	}

	public async ValueTask<ContinuousValue> GetBrightnessAsync(CancellationToken cancellationToken)
	{
		var result = await _i2cTransport.GetVcpFeatureAsync((byte)VcpCode.Luminance, cancellationToken).ConfigureAwait(false);
		return new(0, result.CurrentValue, result.MaximumValue);
	}

	public ValueTask SetBrightnessAsync(ushort value, CancellationToken cancellationToken) => new(_i2cTransport.SetVcpFeatureAsync((byte)VcpCode.Luminance, value, cancellationToken));

	public async ValueTask<ContinuousValue> GetContrastAsync(CancellationToken cancellationToken)
	{
		var result = await _i2cTransport.GetVcpFeatureAsync((byte)VcpCode.Contrast, cancellationToken).ConfigureAwait(false);
		return new(0, result.CurrentValue, result.MaximumValue);
	}

	public ValueTask SetContrastAsync(ushort value, CancellationToken cancellationToken) => new(_i2cTransport.SetVcpFeatureAsync((byte)VcpCode.Contrast, value, cancellationToken));

	ValueTask IUnifiedLightingFeature.ApplyChangesAsync()
	{
		if (!Volatile.Read(ref _currentEffectChanged)) return ValueTask.CompletedTask;

		return new ValueTask(ApplyChangesAsyncCore());
	}

	private async Task ApplyChangesAsyncCore()
	{
		switch (CurrentEffect)
		{
		case DisabledEffect:
			// This would preserve the current lighting, so it might be worth to consider setting a static color effect here, although the LG app does not do this.
			await _lightingTransport.EnableLightingAsync(false, default).ConfigureAwait(false);
			break;
		case StaticColorPreset1Effect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static1, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static1, default).ConfigureAwait(false);
			break;
		case StaticColorPreset2Effect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static2, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static2, default).ConfigureAwait(false);
			break;
		case StaticColorPreset3Effect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static3, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static3, default).ConfigureAwait(false);
			break;
		case StaticColorPreset4Effect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Static4, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Static4, default).ConfigureAwait(false);
			break;
		case ColorCycleEffect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Peaceful, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Peaceful, default).ConfigureAwait(false);
			break;
		case ColorWaveEffect:
			await _lightingTransport.SetActiveEffectAsync(LightingEffect.Dynamic, default).ConfigureAwait(false);
			await _lightingTransport.EnableLightingEffectAsync(LightingEffect.Dynamic, default).ConfigureAwait(false);
			break;
		}
		Volatile.Write(ref _currentEffectChanged, false);
	}

	// TODO: Use a lock for effect setting.
	private ILightingEffect CurrentEffect
	{
		get => Volatile.Read(ref _currentEffect);
		set
		{
			_currentEffect = value;
			Volatile.Write(ref _currentEffectChanged, true);
		}
	}

	bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

	Guid ILightingZone.ZoneId => LightingZoneGuid;

	ILightingEffect ILightingZone.GetCurrentEffect() => _currentEffect;

	void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => CurrentEffect = DisabledEffect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset1Effect>.ApplyEffect(in StaticColorPreset1Effect effect) => CurrentEffect = StaticColorPreset1Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset2Effect>.ApplyEffect(in StaticColorPreset2Effect effect) => CurrentEffect = StaticColorPreset2Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset3Effect>.ApplyEffect(in StaticColorPreset3Effect effect) => CurrentEffect = StaticColorPreset3Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset4Effect>.ApplyEffect(in StaticColorPreset4Effect effect) => CurrentEffect = StaticColorPreset4Effect.SharedInstance;
	void ILightingZoneEffect<ColorCycleEffect>.ApplyEffect(in ColorCycleEffect effect) => CurrentEffect = ColorCycleEffect.SharedInstance;
	void ILightingZoneEffect<ColorWaveEffect>.ApplyEffect(in ColorWaveEffect effect) => CurrentEffect = ColorWaveEffect.SharedInstance;

	bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<ColorCycleEffect>.TryGetCurrentEffect(out ColorCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<ColorWaveEffect>.TryGetCurrentEffect(out ColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset1Effect>.TryGetCurrentEffect(out StaticColorPreset1Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset2Effect>.TryGetCurrentEffect(out StaticColorPreset2Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset3Effect>.TryGetCurrentEffect(out StaticColorPreset3Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset4Effect>.TryGetCurrentEffect(out StaticColorPreset4Effect effect) => CurrentEffect.TryGetEffect(out effect);
}
