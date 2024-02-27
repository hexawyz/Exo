using System.Buffers;
using System.Collections.Immutable;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Mccs;
using DeviceTools.HumanInterfaceDevices;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.LightingFeatures;
using Exo.Features.MonitorFeatures;
using Exo.I2C;
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
	ILgMonitorDisplayStreamCompressionVersionFeature
{
	private static readonly AsyncLock CreationLock = new();

	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[MonitorId("GSM5BBF")]
	[MonitorId("GSM5BEE")]
	[MonitorId("GSM5BC0")]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		Edid edid,
		II2CBus i2cBus,
		CancellationToken cancellationToken
	)
	{
		var info = DeviceDatabase.GetMonitorInformationFromMonitorProductId(edid.ProductId);
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
	[ProductId(VendorIdSource.Usb, DeviceDatabase.LgUsbVendorId, 0x9A8A)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
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

			var vcpResponse = await ddc.GetVcpFeatureWithRetryAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
			ushort scalerVersion = vcpResponse.CurrentValue;
			var data = ArrayPool<byte>.Shared.Rent(1000);

			// Reads the USB product ID.
			//await ddc.SendLgCustomCommandWithRetryAsync(0xC8, 0x00, data.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
			var pid = await ddc.GetVcpFeatureWithRetryAsync(0xA1, cancellationToken).ConfigureAwait(false);

			// This special call will return various data, including a byte representing the DSC firmware version. (In BCD format)
			await ddc.SetLgCustomWithRetryAsync(0xC9, 0x06, data.AsMemory(0, 9), cancellationToken).ConfigureAwait(false);
			byte dscVersion = data[1];

			// This special call will return the monitor model name. We can use it to match extra device information and to build the friendly name.
			await ddc.GetLgCustomWithRetryAsync(0xCA, data.AsMemory(0, 10), cancellationToken).ConfigureAwait(false);
			string modelName = Encoding.ASCII.GetString(data.AsSpan(0, data.AsSpan(0, 10).IndexOf((byte)0)));

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

			var info = DeviceDatabase.GetMonitorInformationFromModelName(modelName);

			// For now, hardcode the lighting for 27GP950. It will be relatively to support other monitors, but we need to make sure that everything works properly.
			UltraGearLightingFeatures? lightingFeatures = null;
			if (info.ModelName == "27GP950")
			{
				var lightingTransport = new UltraGearLightingTransport(new HidFullDuplexStream(lightingDeviceInterfaceName), loggerFactory.CreateLogger<UltraGearLightingTransport>());

				byte ledCount = await lightingTransport.GetLedCountAsync(cancellationToken).ConfigureAwait(false);
				var activeEffect = await lightingTransport.GetActiveEffectAsync(cancellationToken).ConfigureAwait(false);
				var lightingStatus = await lightingTransport.GetLightingStatusAsync(cancellationToken).ConfigureAwait(false);
				// In the current model, we don't really have a use for the current menu selection if lighting is disabled, so we can just override the active effect here to force the effect to disabled.
				if (!lightingStatus.IsLightingEnabled) activeEffect = 0;

				lightingFeatures = new(lightingTransport, ledCount, activeEffect, lightingStatus.CurrentBrightnessLevel, lightingStatus.MinimumBrightnessLevel, lightingStatus.MaximumBrightnessLevel);
			}

			return new DriverCreationResult<SystemDevicePath>
			(
				keys,
				new LgMonitorDriver
				(
					ddc,
					lightingFeatures,
					info.DeviceIds,
					version,
					scalerVersion,
					dscVersion,
					rawCapabilities,
					parsedCapabilities,
					"LG " + info.ModelName,
					new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{modelName}", serialNumber)
				),
				null
			);
		}
	}

	private readonly LgDisplayDataChannelWithRetry _ddc;
	private readonly ImmutableArray<DeviceId> _deviceIds;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private readonly byte[] _rawCapabilities;
	private readonly MonitorCapabilities _parsedCapabilities;
	private readonly UltraGearLightingFeatures? _ultraGearLightingFeatures;
	private readonly IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureCollection<ILgMonitorDeviceFeature> _lgMonitorFeatures;
	private readonly IDeviceFeatureCollection<IDeviceFeature> _allFeatures;

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private LgMonitorDriver
	(
		LgDisplayDataChannelWithRetry ddc,
		UltraGearLightingFeatures? lightingFeatures,
		ImmutableArray<DeviceId> deviceIds,
		ushort nxpVersion,
		ushort scalerVersion,
		byte dscVersion,
		byte[] rawCapabilities,
		MonitorCapabilities parsedCapabilities,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(friendlyName, configurationKey)
	{
		_ddc = ddc;
		_ultraGearLightingFeatures = lightingFeatures;
		_deviceIds = deviceIds;
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
			IMonitorBrightnessFeature,
			IMonitorContrastFeature,
			IMonitorSpeakerAudioVolumeFeature>(this);
		_lightingFeatures = _ultraGearLightingFeatures is not null ?
			FeatureCollection.Create<
				ILightingDeviceFeature,
				UltraGearLightingFeatures,
				IUnifiedLightingFeature,
				ILightingDeferredChangesFeature,
				ILightingBrightnessFeature>(_ultraGearLightingFeatures) :
				FeatureCollection.Empty<ILightingDeviceFeature>();
		_lgMonitorFeatures = FeatureCollection.Create<
			ILgMonitorDeviceFeature,
			LgMonitorDriver,
			ILgMonitorScalerVersionFeature,
			ILgMonitorNxpVersionFeature,
			ILgMonitorDisplayStreamCompressionVersionFeature>(this);
		var baseFeatures = FeatureCollection.Create<IDeviceFeature, LgMonitorDriver, IDeviceSerialNumberFeature, IDeviceIdFeature, IDeviceIdsFeature>(this);
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

	ImmutableArray<DeviceId> IDeviceIdsFeature.DeviceIds => _deviceIds;
	int? IDeviceIdsFeature.MainDeviceIdIndex => 0;
	DeviceId IDeviceIdFeature.DeviceId => _deviceIds[0];
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	public override async ValueTask DisposeAsync()
	{
		await _ddc.DisposeAsync().ConfigureAwait(false);
		if (_ultraGearLightingFeatures is { } lightingFeatures)
		{
			await lightingFeatures.DisposeAsync().ConfigureAwait(false);
		}
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
}
