using System;
using System.Buffers;
using System.Collections.Concurrent;
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
	IDeviceDriver<IBaseDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceDriver<ILgMonitorDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceIdFeature,
	IDeviceIdsFeature,
	INotifyFeaturesChanged,
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
	// The (HID) I2C protocol can occasionally fail, so we want to retry our requests at least once.
	private const int I2CRetryCount = 1;

	private static readonly ConcurrentDictionary<string, LgMonitorDriver> DriversBySerialNumber = new();

	[ExclusionCategory(typeof(LgMonitorDriver))]
	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[MonitorId("GSM5BBF")]
	[MonitorId("GSM5BEE")]
	[MonitorId("GSM5BC0")]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ImmutableArray<SystemDevicePath> keys,
		Edid edid,
		II2CBus i2cBus,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (edid.SerialNumber is null) throw new InvalidOperationException("The monitor doesn't have a serial number.");

		if (!DriversBySerialNumber.TryGetValue(edid.SerialNumber, out var driver))
		{
			ushort scalerVersion;
			byte[] rawCapabilities;
			await using (var ddc = new LgDisplayDataChannelWithRetry(i2cBus, false, I2CRetryCount))
			{
				var vcpResponse = await ddc.GetVcpFeatureWithRetryAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
				scalerVersion = vcpResponse.CurrentValue;
				byte[] data = ArrayPool<byte>.Shared.Rent(1000);
				try
				{
					ushort length = await ddc.GetCapabilitiesAsync(data, cancellationToken).ConfigureAwait(false);
					rawCapabilities = data.AsSpan(0, data.AsSpan(0, length).IndexOf((byte)0)).ToArray();
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(data);
				}
			}

			if (!MonitorCapabilities.TryParse(rawCapabilities, out var parsedCapabilities))
			{
				throw new InvalidOperationException($@"Could not parse monitor capabilities. Value was: ""{Encoding.ASCII.GetString(rawCapabilities)}"".");
			}

			var info = DeviceDatabase.GetMonitorInformationFromMonitorProductId(edid.ProductId);

			// NB: We will not always get the same top level device name depending on which connection is initialized first (USB or one of the multiple display connections).
			// TODO: See if something must be done about the main device name. (Maybe reuse the SN in that case? Anyway, the ID SN from EDID would be part of the Windows device name)
			driver = new LgMonitorDriver
			(
				null,
				info.DeviceIds,
				0,
				scalerVersion,
				0,
				rawCapabilities,
				parsedCapabilities,
				"LG " + info.ModelName,
				new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{info.ModelName}", edid.SerialNumber)
			);
		}
		else
		{
			driver.CompositeI2cBus.AddBus(i2cBus);
		}

		return new(keys, driver, new ConnectedMonitorFacet(driver, i2cBus));
	}

	[ExclusionCategory(typeof(LgMonitorDriver))]
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

		byte sessionId = (byte)Random.Shared.Next(1, 256);
		var i2cBus = await HidI2CTransport.CreateAsync
		(
			new HidFullDuplexStream(i2cDeviceInterfaceName),
			sessionId,
			loggerFactory.CreateLogger<HidI2CTransport>(),
			cancellationToken
		).ConfigureAwait(false);

		ushort scalerVersion;
		byte dscVersion;
		string modelName;
		string serialNumber;
		byte[] rawCapabilities;
		await using (var ddc = new LgDisplayDataChannelWithRetry(i2cBus, false, I2CRetryCount))
		{
			var vcpResponse = await ddc.GetVcpFeatureWithRetryAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
			scalerVersion = vcpResponse.CurrentValue;
			byte[] data = ArrayPool<byte>.Shared.Rent(1000);
			try
			{
				// Reads the USB product ID.
				//await ddc.SendLgCustomCommandWithRetryAsync(0xC8, 0x00, data.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
				//var pid = await ddc.GetVcpFeatureWithRetryAsync(0xA1, cancellationToken).ConfigureAwait(false);

				// This special call will return various data, including a byte representing the DSC firmware version. (In BCD format)
				await ddc.SetLgCustomWithRetryAsync(0xC9, 0x06, data.AsMemory(0, 9), cancellationToken).ConfigureAwait(false);
				dscVersion = data[1];

				// This special call will return the monitor model name. We can use it to match extra device information and to build the friendly name.
				await ddc.GetLgCustomWithRetryAsync(0xCA, data.AsMemory(0, 10), cancellationToken).ConfigureAwait(false);
				modelName = Encoding.ASCII.GetString(data.AsSpan(0, data.AsSpan(0, 10).IndexOf((byte)0)));

				// This special call will return the serial number. The monitor here has a 12 character long serial number, but let's hope this is fixed length.
				await ddc.GetLgCustomWithRetryAsync(0x78, data.AsMemory(0, 12), cancellationToken).ConfigureAwait(false);
				serialNumber = Encoding.ASCII.GetString(data.AsSpan(..12));

				ushort length = await ddc.GetCapabilitiesAsync(data, cancellationToken).ConfigureAwait(false);
				rawCapabilities = data.AsSpan(0, data.AsSpan(0, length).IndexOf((byte)0)).ToArray();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(data);
			}
		}

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

		if (!DriversBySerialNumber.TryGetValue(serialNumber, out var driver))
		{
			driver = new LgMonitorDriver
			(
				lightingFeatures,
				info.DeviceIds,
				version,
				scalerVersion,
				dscVersion,
				rawCapabilities,
				parsedCapabilities,
				"LG " + info.ModelName,
				new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{modelName}", serialNumber)
			);
			if (!DriversBySerialNumber.TryAdd(serialNumber, driver))
			{
				await driver.DisposeAsync().ConfigureAwait(false);
				await i2cBus.DisposeAsync().ConfigureAwait(false);
				if (lightingFeatures is not null)
				{
					await lightingFeatures.DisposeAsync().ConfigureAwait(false);
				}

				throw new InvalidOperationException($"""A driver with the serial number "{serialNumber}" was already registered.""");
			}
		}
		else if (lightingFeatures is not null)
		{
			driver.UpdateFeatures(lightingFeatures);
		}

		driver.CompositeI2cBus.SetUsbBus(i2cBus);

		return new DriverCreationResult<SystemDevicePath>(keys, driver, lightingFeatures is not null ? new UsbI2cWithLightingFacet(driver, i2cBus, lightingFeatures) : new UsbI2cFacet(driver, i2cBus));
	}

	private abstract class Facet : IAsyncDisposable
	{
		private LgMonitorDriver? _driver;

		protected Facet(LgMonitorDriver driver) => _driver = driver;

		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _driver, null) is { } driver)
			{
				await DisposeAsync(driver);
			}
		}

		protected abstract ValueTask DisposeAsync(LgMonitorDriver driver);
	}

	private sealed class ConnectedMonitorFacet : Facet
	{
		private readonly II2CBus _i2cBus;

		public ConnectedMonitorFacet(LgMonitorDriver driver, II2CBus i2cBus)
			: base(driver)
		{
			_i2cBus = i2cBus;
		}

		protected override ValueTask DisposeAsync(LgMonitorDriver driver)
		{
			driver.CompositeI2cBus.RemoveBus(_i2cBus);
			return ValueTask.CompletedTask;
		}
	}

	private sealed class UsbI2cFacet : Facet
	{
		private readonly HidI2CTransport _i2cBus;

		public UsbI2cFacet(LgMonitorDriver driver, HidI2CTransport i2cBus)
			: base(driver)
		{
			_i2cBus = i2cBus;
		}

		protected override ValueTask DisposeAsync(LgMonitorDriver driver)
		{
			driver.CompositeI2cBus.UnsetUsbBus(_i2cBus);
			return ValueTask.CompletedTask;
		}
	}

	private sealed class UsbI2cWithLightingFacet : Facet
	{
		private readonly HidI2CTransport _i2cBus;
		private readonly UltraGearLightingFeatures _ultraGearLightingFeatures;

		public UsbI2cWithLightingFacet(LgMonitorDriver driver, HidI2CTransport i2cBus, UltraGearLightingFeatures ultraGearLightingFeatures) : base(driver)
		{
			_i2cBus = i2cBus;
			_ultraGearLightingFeatures = ultraGearLightingFeatures;
		}

		protected override async ValueTask DisposeAsync(LgMonitorDriver driver)
		{
			driver.CompositeI2cBus.UnsetUsbBus(_i2cBus);
			driver.UpdateFeatures(null);
			await _ultraGearLightingFeatures.DisposeAsync().ConfigureAwait(false);
		}
	}

	private readonly LgDisplayDataChannelWithRetry _ddc;
	private readonly ImmutableArray<DeviceId> _deviceIds;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private readonly byte[] _rawCapabilities;
	private readonly MonitorCapabilities _parsedCapabilities;
	private IDeviceFeatureCollection<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureCollection<IMonitorDeviceFeature> _monitorFeatures;
	private readonly IDeviceFeatureCollection<ILgMonitorDeviceFeature> _lgMonitorFeatures;
	private readonly IDeviceFeatureCollection<IBaseDeviceFeature> _baseFeatures;
	private CompositeI2cBus CompositeI2cBus { get; }

	private void UpdateFeatures(UltraGearLightingFeatures? ultraGearLightingFeatures)
	{
		var lightingFeatures = ultraGearLightingFeatures is not null ?
			FeatureCollection.Create<
				ILightingDeviceFeature,
				UltraGearLightingFeatures,
				IUnifiedLightingFeature,
				ILightingDeferredChangesFeature,
				ILightingBrightnessFeature>(ultraGearLightingFeatures) :
				FeatureCollection.Empty<ILightingDeviceFeature>();
		Volatile.Write(ref _lightingFeatures, lightingFeatures);
		FeaturesChanged?.Invoke(this, EventArgs.Empty);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	public event EventHandler FeaturesChanged;

	private LgMonitorDriver
	(
		UltraGearLightingFeatures? ultraGearLightingFeatures,
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
		CompositeI2cBus = new();
		_ddc = new LgDisplayDataChannelWithRetry(CompositeI2cBus, true, I2CRetryCount);
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
		_lgMonitorFeatures = FeatureCollection.Create<
			ILgMonitorDeviceFeature,
			LgMonitorDriver,
			ILgMonitorScalerVersionFeature,
			ILgMonitorNxpVersionFeature,
			ILgMonitorDisplayStreamCompressionVersionFeature>(this);
		_baseFeatures = FeatureCollection.Create<IBaseDeviceFeature, LgMonitorDriver, IDeviceSerialNumberFeature, IDeviceIdFeature, IDeviceIdsFeature>(this);
		UpdateFeatures(ultraGearLightingFeatures);
	}

	// The firmware archive explicitly names the SV, DV and NV as "Scaler", "DSC" and "NXP"
	// DSC versions are expanded in a single byte, seemingly in BCD format. So, 0x44 for version 44, and 0x51 for version 51.
	public SimpleVersion FirmwareNxpVersion => new((byte)(_nxpVersion >>> 8), (byte)_nxpVersion);
	public SimpleVersion FirmwareScalerVersion => new((byte)(_scalerVersion >>> 8), (byte)_scalerVersion);
	public SimpleVersion FirmwareDisplayStreamCompressionVersion => new((byte)((_dscVersion >>> 4) * 10 | _dscVersion & 0x0F), 0);
	public ReadOnlySpan<byte> RawCapabilities => _rawCapabilities;
	public MonitorCapabilities Capabilities => _parsedCapabilities;

	IDeviceFeatureCollection<IBaseDeviceFeature> IDeviceDriver<IBaseDeviceFeature>.Features => _baseFeatures;
	IDeviceFeatureCollection<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;
	IDeviceFeatureCollection<ILgMonitorDeviceFeature> IDeviceDriver<ILgMonitorDeviceFeature>.Features => _lgMonitorFeatures;
	IDeviceFeatureCollection<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => Volatile.Read(ref _lightingFeatures);

	ImmutableArray<DeviceId> IDeviceIdsFeature.DeviceIds => _deviceIds;
	int? IDeviceIdsFeature.MainDeviceIdIndex => 0;
	DeviceId IDeviceIdFeature.DeviceId => _deviceIds[0];
	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	public override async ValueTask DisposeAsync()
	{
		try
		{
			await _ddc.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			DriversBySerialNumber.TryRemove(new(ConfigurationKey.UniqueId!, this));
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
