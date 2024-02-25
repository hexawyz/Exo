using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Lg.Monitors.LightingEffects;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Features.MonitorFeatures;
using Exo.I2C;
using Exo.Lighting;
using Exo.Lighting.Effects;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Lg.Monitors;

public class LgMonitorDriver :
	Driver,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<ILgMonitorDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceIdFeature,
	IDeviceIdsFeature,
	IDeviceSerialNumberFeature,
	IRawVcpFeature,
	IMonitorBrightnessFeature,
	IMonitorContrastFeature,
	IMonitorSpeakerAudioVolumeFeature,
	IMonitorCapabilitiesFeature,
	IMonitorRawCapabilitiesFeature,
	ILgMonitorScalerVersionFeature,
	ILgMonitorNxpVersionFeature,
	ILgMonitorDisplayStreamCompressionVersionFeature,
	IUnifiedLightingFeature,
	ILightingDeferredChangesFeature,
	ILightingBrightnessFeature,
	IAddressableLightingZone<RgbColor>,
	ILightingZoneEffect<DisabledEffect>,
	//ILightingZoneEffect<StaticColorEffect>,
	ILightingZoneEffect<StaticColorPreset1Effect>,
	ILightingZoneEffect<StaticColorPreset2Effect>,
	ILightingZoneEffect<StaticColorPreset3Effect>,
	ILightingZoneEffect<StaticColorPreset4Effect>,
	ILightingZoneEffect<ColorCycleEffect>,
	ILightingZoneEffect<ColorWaveEffect>
{
	private static readonly AsyncLock CreationLock = new();

	private const int LgVendorId = 0x043E;

	private static readonly Guid LightingZoneGuid = new(0x7105A4FA, 0x2235, 0x49FC, 0xA7, 0x5A, 0xFD, 0x0D, 0xEC, 0x13, 0x51, 0x99);

	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[MonitorId("GSM5BBF")]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		Edid edid,
		II2CBus i2cBus,
		CancellationToken cancellationToken
	)
	{
		//using (await CreationLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		//{
		//	var ddc = new LgDisplayDataChannelWithRetry(i2cBus, true, 1);

		//	var data = ArrayPool<byte>.Shared.Rent(1000);
		//	try
		//	{
		//		//var z = await ddc.GetVcpFeatureAsync(0xCA, cancellationToken).ConfigureAwait(false);
		//		//await ddc.GetLgCustomWithRetryAsync(0xCA, data.AsMemory(0, 11), cancellationToken).ConfigureAwait(false);
		//	}
		//	catch (Exception ex)
		//	{
		//	}
		//	finally
		//	{
		//		ArrayPool<byte>.Shared.Return(data);
		//	}
		//}

		throw new NotImplementedException("TODO");
	}

	[DiscoverySubsystem<HidDiscoverySubsystem>]
	[ProductId(VendorIdSource.Usb, LgVendorId, 0x9A8A)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		ushort productId,
		ushort version,
		ImmutableArray<DeviceObjectInformation> deviceInterfaces,
		ImmutableArray<DeviceObjectInformation> devices,
		string topLevelDeviceName,
		ILoggerFactory loggerFactory,
		CancellationToken cancellationToken
	)
	{
		using (await CreationLock.WaitAsync(cancellationToken).ConfigureAwait(false))
		{
			string? i2cDeviceInterfaceName = null;
			string? lightingDeviceInterfaceName = null;

			for (int i = 0; i < deviceInterfaces.Length; i++)
			{
				var deviceInterface = deviceInterfaces[i];

				// Skip non-HID device interfaces.
				if (!deviceInterface.Properties.TryGetValue(Properties.System.Devices.InterfaceClassGuid.Key, out Guid interfaceClassGuid) || interfaceClassGuid != DeviceInterfaceClassGuids.Hid)
				{
					continue;
				}

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
				}
				else if (usagePage == 0xFF01 && usageId == 0x01)
				{
					lightingDeviceInterfaceName = deviceInterface.Id;
				}
			}

			if (i2cDeviceInterfaceName is null || lightingDeviceInterfaceName is null)
			{
				throw new InvalidOperationException($"Could not find device interfaces with correct HID usages on the device {topLevelDeviceName}.");
			}

			var lightingTransport = new UltraGearLightingTransport(new HidFullDuplexStream(lightingDeviceInterfaceName), loggerFactory.CreateLogger<UltraGearLightingTransport>());

			byte ledCount = await lightingTransport.GetLedCountAsync(cancellationToken).ConfigureAwait(false);
			var activeEffect = await lightingTransport.GetActiveEffectAsync(cancellationToken).ConfigureAwait(false);
			var lightingStatus = await lightingTransport.GetLightingStatusAsync(cancellationToken).ConfigureAwait(false);
			// In the current model, we don't really have a use for the current menu selection if lighting is disabled, so we can just override the active effect here to force the effect to disabled.
			if (!lightingStatus.IsLightingEnabled) activeEffect = 0;

			// The HID I2C protocol can occasionally fail, so we want to retry our requests at least once.
			// NB: I didn't dig up the reason for failures, but I saw them happen a few times. It could just be concurrent accesses with the OS or GPU. (Because I don't think the bus can be locked?)
			const int I2CRetryCount = 1;

			byte sessionId = (byte)Random.Shared.Next(1, 256);
			var i2cBus = await HidI2CTransport.CreateAsync
			(
				new HidFullDuplexStream(i2cDeviceInterfaceName),
				sessionId,
				loggerFactory.CreateLogger<HidI2CTransport>(),
				cancellationToken
			).ConfigureAwait(false);

			var ddc = new LgDisplayDataChannelWithRetry(i2cBus, true, I2CRetryCount);

			//var edid = await ddc.GetEdidAsync(cancellationToken).ConfigureAwait(false);

			var vcpResponse = await ddc.GetVcpFeatureWithRetryAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
			ushort scalerVersion = vcpResponse.CurrentValue;
			var data = ArrayPool<byte>.Shared.Rent(1000);

			// Reads the USB product ID.
			//await ddc.SendLgCustomCommandWithRetryAsync(0xC8, 0x00, data.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
			var pid = await ddc.GetVcpFeatureWithRetryAsync(0xA1, cancellationToken).ConfigureAwait(false);

			// This special call will return various data, including a byte representing the DSC firmware version. (In BCD format)
			await ddc.SetLgCustomWithRetryAsync(0xC9, 0x06, data.AsMemory(0, 9), cancellationToken).ConfigureAwait(false);
			byte dscVersion = data[1];

			// This special call will return the monitor model name. We can then use it as the friendly name.
			await ddc.GetLgCustomWithRetryAsync(0xCA, data.AsMemory(0, 10), cancellationToken).ConfigureAwait(false);
			friendlyName = "LG " + Encoding.ASCII.GetString(data.AsSpan(0, data.AsSpan(0, 10).IndexOf((byte)0)));

			// This special call will return the serial number. The monitor here has a 12 character long serial number, but let's hope this is fixed length.
			await ddc.GetLgCustomWithRetryAsync(0x78, data.AsMemory(0, 12), cancellationToken).ConfigureAwait(false);
			string serialNumber = Encoding.ASCII.GetString(data.AsSpan(..12));

			var length = await ddc.GetCapabilitiesAsync(data, cancellationToken).ConfigureAwait(false);
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
			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new LgMonitorDriver
				(
					ddc,
					lightingTransport,
					productId,
					version,
					scalerVersion,
					dscVersion,
					ledCount,
					activeEffect,
					lightingStatus.CurrentBrightnessLevel,
					lightingStatus.MinimumBrightnessLevel,
					lightingStatus.MaximumBrightnessLevel,
					rawCapabilities,
					parsedCapabilities,
					friendlyName,
					new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{productId:X4}", serialNumber)
				),
				null
			);
		}
	}

	const int StateEffectChanged = 0x01;
	const int StateBrightnessChanged = 0x02;
	const int StateLocked = 0x100;

	private readonly LgDisplayDataChannelWithRetry _ddc;
	private readonly UltraGearLightingTransport _lightingTransport;
	private ILightingEffect _currentEffect;
	// Protect against concurrent accesses using a combination of lock and state.
	// Concurrent accesses should not happen, but I believe this is not enforced anywhere yet. (i.e. In the lighting service)
	// The protection here is quite simple and will prevent incorrect uses at the cost of raising exceptions.
	// The state value can only change within the lock, and if the state is locked, an exception must be thrown.
	// The state is locked when the ApplyChangesAsync method is executing, and unlocked afterwards.
	private readonly object _lock;
	private int _state;
	private readonly ushort _productId;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private readonly byte _ledCount;
	private readonly byte _appliedBrightness;
	private byte _currentBrightness;
	private readonly byte _minimumBrightness;
	private readonly byte _maximumBrightness;
	private readonly byte[] _rawCapabilities;
	private readonly MonitorCapabilities _parsedCapabilities;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureCollection<ILgMonitorDeviceFeature> _lgMonitorFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private LgMonitorDriver
	(
		LgDisplayDataChannelWithRetry ddc,
		UltraGearLightingTransport lightingTransport,
		ushort productId,
		ushort nxpVersion,
		ushort scalerVersion,
		byte dscVersion,
		byte ledCount,
		LightingEffect activeEffect,
		byte currentBrightness,
		byte minimumBrightness,
		byte maximumBrightness,
		byte[] rawCapabilities,
		MonitorCapabilities parsedCapabilities,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_ddc = ddc;
		_lightingTransport = lightingTransport;
		_currentEffect = activeEffect switch
		{
			LightingEffect.Static1 => StaticColorPreset1Effect.SharedInstance,
			LightingEffect.Static2 => StaticColorPreset2Effect.SharedInstance,
			LightingEffect.Static3 => StaticColorPreset3Effect.SharedInstance,
			LightingEffect.Static4 => StaticColorPreset4Effect.SharedInstance,
			LightingEffect.Peaceful => ColorCycleEffect.SharedInstance,
			LightingEffect.Dynamic => ColorWaveEffect.SharedInstance,
			// I'm unsure what would happen here if the current effect was reported as audio sync or video sync ?
			// If these modes are reported, we need to explicitly disable the lighting.
			_ => DisabledEffect.SharedInstance,
		};
		_lock = new();
		_productId = productId;
		_nxpVersion = nxpVersion;
		_scalerVersion = scalerVersion;
		_dscVersion = dscVersion;
		_ledCount = ledCount;
		_appliedBrightness = currentBrightness;
		_currentBrightness = currentBrightness;
		_minimumBrightness = minimumBrightness;
		_maximumBrightness = maximumBrightness;
		_rawCapabilities = rawCapabilities;
		_parsedCapabilities = parsedCapabilities;
		_monitorFeatures = FeatureCollection.Create<
			IMonitorDeviceFeature,
			LgMonitorDriver,
			IMonitorRawCapabilitiesFeature,
			IMonitorCapabilitiesFeature,
			IRawVcpFeature,
			IMonitorBrightnessFeature,
			IMonitorContrastFeature,
			IMonitorSpeakerAudioVolumeFeature>(this);
		_lightingFeatures = FeatureCollection.Create<
			ILightingDeviceFeature,
			LgMonitorDriver,
			IUnifiedLightingFeature,
			ILightingDeferredChangesFeature,
			ILightingBrightnessFeature>(this);
		_lgMonitorFeatures = FeatureCollection.Create<
			ILgMonitorDeviceFeature,
			LgMonitorDriver,
			ILgMonitorScalerVersionFeature,
			ILgMonitorNxpVersionFeature,
			ILgMonitorDisplayStreamCompressionVersionFeature>(this);
		var baseFeatures = FeatureCollection.Create<IDeviceFeature, LgMonitorDriver, IDeviceSerialNumberFeature, IDeviceIdFeature>(this);
		_allFeatures = FeatureCollection.CreateMerged(_lightingFeatures, _monitorFeatures, _lgMonitorFeatures, baseFeatures);
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
		await _ddc.DisposeAsync().ConfigureAwait(false);
		await _lightingTransport.DisposeAsync().ConfigureAwait(false);
	}

	public ValueTask SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken) => _ddc.SetVcpFeatureAsync(vcpCode, value, cancellationToken);

	public async ValueTask<VcpFeatureReply> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken)
	{
		var result = await _ddc.GetVcpFeatureAsync(vcpCode, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, result.MaximumValue, result.IsMomentary);
	}

	public async ValueTask<ContinuousValue> GetBrightnessAsync(CancellationToken cancellationToken)
	{
		var result = await _ddc.GetVcpFeatureAsync((byte)VcpCode.Luminance, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, 0, result.MaximumValue);
	}

	public ValueTask SetBrightnessAsync(ushort value, CancellationToken cancellationToken) => _ddc.SetVcpFeatureAsync((byte)VcpCode.Luminance, value, cancellationToken);

	public async ValueTask<ContinuousValue> GetContrastAsync(CancellationToken cancellationToken)
	{
		var result = await _ddc.GetVcpFeatureAsync((byte)VcpCode.Contrast, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, 0, result.MaximumValue);
	}

	public ValueTask SetContrastAsync(ushort value, CancellationToken cancellationToken) => _ddc.SetVcpFeatureAsync((byte)VcpCode.Contrast, value, cancellationToken);

	async ValueTask<ContinuousValue> IMonitorSpeakerAudioVolumeFeature.GetVolumeAsync(CancellationToken cancellationToken)
	{
		var result = await _ddc.GetVcpFeatureAsync((byte)VcpCode.AudioSpeakerVolume, cancellationToken).ConfigureAwait(false);
		return new(result.CurrentValue, 0, result.MaximumValue);
	}

	ValueTask IMonitorSpeakerAudioVolumeFeature.SetVolumeAsync(ushort value, CancellationToken cancellationToken)
		=> _ddc.SetVcpFeatureAsync((byte)VcpCode.AudioSpeakerVolume, value, cancellationToken);

	ValueTask ILightingDeferredChangesFeature.ApplyChangesAsync()
	{
		int state;

		lock (_lock)
		{
			EnsureStateIsUnlocked();

			if (_state == 0) return ValueTask.CompletedTask;

			state = _state;

			_state = state | StateLocked;
		}

		return new ValueTask(ApplyChangesAsyncCore(state));
	}

	// This method does not actually execute from within the lock (we would need an async lock), but the state has the lock flag preventing concurrent accesses.
	private async Task ApplyChangesAsyncCore(int state)
	{
		if ((state & StateBrightnessChanged) != 0)
		{
			await _lightingTransport.SetLightingStatusAsync(_currentEffect is not DisabledEffect, _currentBrightness, default).ConfigureAwait(false);
		}

		if ((state & StateEffectChanged) != 0)
		{
			switch (CurrentEffect)
			{
			case DisabledEffect:
				// This would preserve the current lighting, so it might be worth to consider setting a static color effect here, although the LG app does not do this.
				await _lightingTransport.SetLightingStatusAsync(false, 0, default).ConfigureAwait(false);
				break;
			//case StaticColorEffect staticColor:
			//	// We implement the static color effect using the video sync mode, which is essentially addressable mode.
			//	// Maybe there are subtle differences with true addressable mode, but I'm not aware of them. (Is there even any actual difference between audio & video modes ?)
			//	await _lightingTransport.SetActiveEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
			//	await _lightingTransport.EnableLightingEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
			//	await _lightingTransport.SetVideoSyncColors(staticColor.Color, 36, default).ConfigureAwait(false);
			//	break;
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
			case AddressableColorEffect:
				// NB: It seems that the dynamic effect will self-disable after a while if not updated. (10s)
				// TODO: Find an acceptable way to manage this. Either force keep-alive the effect or track long delays between updates to re-enable the effect.
				await _lightingTransport.SetActiveEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);
				await _lightingTransport.EnableLightingEffectAsync(LightingEffect.VideoSync, default).ConfigureAwait(false);

				//_ = TestDynamicEffectAsync();

				break;
			}
		}
		Volatile.Write(ref _state, 0);
	}

	// Just used to validate that the effect works.
	// This is a very basic effect that should probably be moved into its own class later.
	private async Task TestDynamicEffectAsync()
	{
		var colors = new RgbColor[40];

		for (int i = 0; i < 10; i++)
		{
			int j = 4 * i;
			colors[j + 0] = new(0x00, 0x40, 0x40);
			colors[j + 1] = new(0x00, 0xFF, 0xFF);
			colors[j + 2] = new(0x00, 0x40, 0x40);
			colors[j + 3] = new(0x00, 0x00, 0x00);
		}

		int k = 0;
		while (true)
		{
			await this.SetColorsAsync(colors.AsSpan(k, 36));
			k = (k + 1) & 3;
			await Task.Delay(150);
		}
	}

	private ILightingEffect CurrentEffect
	{
		get => Volatile.Read(ref _currentEffect);
		set
		{
			lock (_lock)
			{
				EnsureStateIsUnlocked();
				_currentEffect = value;
				_state |= StateEffectChanged;
			}
		}
	}

	private void EnsureStateIsUnlocked()
	{
		if ((_state & StateLocked) != 0) throw new InvalidOperationException("A concurrent operation is currently being executed.");
	}

	bool IUnifiedLightingFeature.IsUnifiedLightingEnabled => true;

	Guid ILightingZone.ZoneId => LightingZoneGuid;

	ILightingEffect ILightingZone.GetCurrentEffect() => _currentEffect;

	void ILightingZoneEffect<DisabledEffect>.ApplyEffect(in DisabledEffect effect) => CurrentEffect = DisabledEffect.SharedInstance;
	//void ILightingZoneEffect<StaticColorEffect>.ApplyEffect(in StaticColorEffect effect) => CurrentEffect = effect;
	void ILightingZoneEffect<StaticColorPreset1Effect>.ApplyEffect(in StaticColorPreset1Effect effect) => CurrentEffect = StaticColorPreset1Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset2Effect>.ApplyEffect(in StaticColorPreset2Effect effect) => CurrentEffect = StaticColorPreset2Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset3Effect>.ApplyEffect(in StaticColorPreset3Effect effect) => CurrentEffect = StaticColorPreset3Effect.SharedInstance;
	void ILightingZoneEffect<StaticColorPreset4Effect>.ApplyEffect(in StaticColorPreset4Effect effect) => CurrentEffect = StaticColorPreset4Effect.SharedInstance;
	void ILightingZoneEffect<ColorCycleEffect>.ApplyEffect(in ColorCycleEffect effect) => CurrentEffect = ColorCycleEffect.SharedInstance;
	void ILightingZoneEffect<ColorWaveEffect>.ApplyEffect(in ColorWaveEffect effect) => CurrentEffect = ColorWaveEffect.SharedInstance;
	void ILightingZoneEffect<AddressableColorEffect>.ApplyEffect(in AddressableColorEffect effect) => CurrentEffect = AddressableColorEffect.SharedInstance;

	bool ILightingZoneEffect<DisabledEffect>.TryGetCurrentEffect(out DisabledEffect effect) => CurrentEffect.TryGetEffect(out effect);
	//bool ILightingZoneEffect<StaticColorEffect>.TryGetCurrentEffect(out StaticColorEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<ColorCycleEffect>.TryGetCurrentEffect(out ColorCycleEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<ColorWaveEffect>.TryGetCurrentEffect(out ColorWaveEffect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset1Effect>.TryGetCurrentEffect(out StaticColorPreset1Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset2Effect>.TryGetCurrentEffect(out StaticColorPreset2Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset3Effect>.TryGetCurrentEffect(out StaticColorPreset3Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<StaticColorPreset4Effect>.TryGetCurrentEffect(out StaticColorPreset4Effect effect) => CurrentEffect.TryGetEffect(out effect);
	bool ILightingZoneEffect<AddressableColorEffect>.TryGetCurrentEffect(out AddressableColorEffect effect) => CurrentEffect.TryGetEffect(out effect);

	int IAddressableLightingZone.AddressableLightCount => _ledCount;
	bool IAddressableLightingZone.AllowsRandomAccesses => false;

	ValueTask IAddressableLightingZone<RgbColor>.SetColorsAsync(int index, ReadOnlySpan<RgbColor> colors)
	{
		if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
		if (colors.Length != _ledCount) throw new ArgumentException("The number of colors received is incorrect.");

		return new ValueTask(_lightingTransport.SetVideoSyncColors(colors, default));
	}

	DeviceId IDeviceIdFeature.DeviceId => new(DeviceIdSource.Usb, VendorIdSource.Usb, LgVendorId, _productId, _nxpVersion);

	byte ILightingBrightnessFeature.MinimumBrightness => _minimumBrightness;
	byte ILightingBrightnessFeature.MaximumBrightness => _maximumBrightness;

	byte ILightingBrightnessFeature.CurrentBrightness
	{
		get => _currentBrightness;
		set
		{
			if (value < _minimumBrightness || value > _maximumBrightness) throw new ArgumentOutOfRangeException(nameof(value));

			lock (_lock)
			{
				EnsureStateIsUnlocked();
				_currentBrightness = value;
				_state |= StateBrightnessChanged;
			}
		}
	}

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	ImmutableArray<DeviceId> IDeviceIdsFeature.DeviceIds { get; }
	int? IDeviceIdsFeature.MainDeviceIdIndex { get; }
}
