using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using DeviceTools.DisplayDevices.Mccs;
using DeviceTools.HumanInterfaceDevices;
using Exo.Devices.Monitors;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Lighting;
using Exo.I2C;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Lg.Monitors;

public class LgMonitorDriver :
	GenericMonitorDriver,
	IDeviceDriver<ILgMonitorDeviceFeature>,
	IDeviceDriver<ILightingDeviceFeature>,
	IDeviceIdsFeature,
	IVariableFeatureSetDeviceFeature,
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
		ILogger<LgMonitorDriver> logger,
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
			ImmutableArray<byte> rawCapabilities;
			await using (var ddc = new LgDisplayDataChannelWithRetry(i2cBus, false, I2CRetryCount))
			{
				var vcpResponse = await ddc.GetVcpFeatureWithRetryAsync((byte)VcpCode.DisplayFirmwareLevel, cancellationToken).ConfigureAwait(false);
				scalerVersion = vcpResponse.CurrentValue;
				byte[] data = ArrayPool<byte>.Shared.Rent(1000);
				try
				{
					ushort length = await ddc.GetCapabilitiesWithRetryAsync(data, cancellationToken).ConfigureAwait(false);
					rawCapabilities = data.AsSpan(0, data.AsSpan(0, length).IndexOf((byte)0)).ToImmutableArray();
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(data);
				}
			}

			var monitorId = new MonitorId(edid.VendorId, edid.ProductId);

			LogRetrievedCapabilities(logger, monitorId, rawCapabilities);

			var info = DeviceDatabase.GetMonitorInformationFromMonitorProductId(edid.ProductId);

			var featureSetBuilder = new MonitorFeatureSetBuilder();

			var genericMonitorDetails = PrepareMonitorFeatures(featureSetBuilder, rawCapabilities, monitorId);

			if (genericMonitorDetails.Capabilities is null)
			{
				throw new InvalidOperationException($@"Could not parse monitor capabilities. Value was: ""{Encoding.ASCII.GetString(rawCapabilities.AsSpan())}"".");
			}

			// NB: We will not always get the same top level device name depending on which connection is initialized first (USB or one of the multiple display connections).
			// TODO: See if something must be done about the main device name. (Maybe reuse the SN in that case? Anyway, the ID SN from EDID would be part of the Windows device name)
			driver = new LgMonitorDriver
			(
				new CompositeI2cBus(),
				featureSetBuilder,
				null,
				info.DeviceIds,
				0,
				scalerVersion,
				0,
				rawCapabilities.AsMemory(),
				genericMonitorDetails.Capabilities,
				info.Features,
				"LG " + info.ModelName,
				new("LGMonitor", topLevelDeviceName, $"LG_Monitor_{info.ModelName}", edid.SerialNumber)
			);

			driver.CompositeI2cBus.AddBus(i2cBus);
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
		ILogger<LgMonitorDriver> logger,
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
		ImmutableArray<byte> rawCapabilities;
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

				ushort length = await ddc.GetCapabilitiesWithRetryAsync(data, cancellationToken).ConfigureAwait(false);
				rawCapabilities = data.AsSpan(0, data.AsSpan(0, length).IndexOf((byte)0)).ToImmutableArray();
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(data);
			}
		}

		var info = DeviceDatabase.GetMonitorInformationFromModelName(modelName);

		// Get the first non-USB device ID for the monitor for the database lookup.
		var monitorDeviceId = info.DeviceIds[info.DeviceIds.Length > 1 && info.DeviceIds[0].Source == DeviceIdSource.Usb ? 1 : 0];
		var monitorId = new MonitorId(PnpVendorId.FromRaw(monitorDeviceId.VendorId), monitorDeviceId.ProductId);

		LogRetrievedCapabilities(logger, monitorId, rawCapabilities);

		var featureSetBuilder = new MonitorFeatureSetBuilder();

		var genericMonitorDetails = PrepareMonitorFeatures(featureSetBuilder, rawCapabilities, monitorId);

		if (genericMonitorDetails.Capabilities is null)
		{
			throw new InvalidOperationException($@"Could not parse monitor capabilities. Value was: ""{Encoding.ASCII.GetString(rawCapabilities.AsSpan())}"".");
		}

		// For now, hardcode the lighting for 27GP950. It will be relatively easy to support other monitors, but we need to make sure that everything works properly.
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
				new CompositeI2cBus(),
				featureSetBuilder,
				lightingFeatures,
				info.DeviceIds,
				version,
				scalerVersion,
				dscVersion,
				rawCapabilities.AsMemory(),
				genericMonitorDetails.Capabilities,
				info.Features,
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

	private readonly ImmutableArray<DeviceId> _deviceIds;
	private readonly ushort _nxpVersion;
	private readonly ushort _scalerVersion;
	private readonly byte _dscVersion;
	private IDeviceFeatureSet<ILightingDeviceFeature> _lightingFeatures;
	private readonly IDeviceFeatureSet<ILgMonitorDeviceFeature> _lgMonitorFeatures;
	private FeatureSetDescription[] _featureSets;
	private CompositeI2cBus CompositeI2cBus { get; }

	[MemberNotNull(nameof(_lightingFeatures))]
	private void UpdateFeatures(UltraGearLightingFeatures? ultraGearLightingFeatures)
	{
		var lightingFeatures = ultraGearLightingFeatures is not null ?
			FeatureSet.Create<
				ILightingDeviceFeature,
				UltraGearLightingFeatures,
				IUnifiedLightingFeature,
				ILightingDeferredChangesFeature,
				ILightingBrightnessFeature>(ultraGearLightingFeatures) :
				FeatureSet.Empty<ILightingDeviceFeature>();
		Volatile.Write(ref _lightingFeatures, lightingFeatures);
		Volatile.Write
		(
			ref _featureSets,
			[
				FeatureSetDescription.CreateStatic<IGenericDeviceFeature>(),
				FeatureSetDescription.CreateStatic<IMonitorDeviceFeature>(),
				FeatureSetDescription.CreateStatic<ILgMonitorDeviceFeature>(),
				FeatureSetDescription.CreateDynamic<ILightingDeviceFeature>(ultraGearLightingFeatures is not null),
			]
		);
		FeatureAvailabilityChanged?.Invoke(this, lightingFeatures);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private event FeatureSetEventHandler FeatureAvailabilityChanged;

	event FeatureSetEventHandler IVariableFeatureSetDeviceFeature.FeatureAvailabilityChanged
	{
		add => FeatureAvailabilityChanged += value;
		remove => FeatureAvailabilityChanged -= value;
	}

	public override ImmutableArray<FeatureSetDescription> FeatureSets => ImmutableCollectionsMarshal.AsImmutableArray(_featureSets);

	public override IDeviceFeatureSet<TFeature> GetFeatureSet<TFeature>()
	{
		System.Collections.IEnumerable GetFeatures()
		{
			if (typeof(TFeature) == typeof(IGenericDeviceFeature)) return GenericFeatures;
			if (typeof(TFeature) == typeof(ILightingDeviceFeature)) return _lightingFeatures;
			if (typeof(TFeature) == typeof(IMonitorDeviceFeature)) return MonitorFeatures;
			if (typeof(TFeature) == typeof(ILgMonitorDeviceFeature)) return _lgMonitorFeatures;

			return FeatureSet.Empty<TFeature>();
		}

		return Unsafe.As<IDeviceFeatureSet<TFeature>>(GetFeatures());
	}

	private LgMonitorDriver
	(
		CompositeI2cBus compositeI2cBus,
		MonitorFeatureSetBuilder monitorFeatureSetBuilder,
		UltraGearLightingFeatures? ultraGearLightingFeatures,
		ImmutableArray<DeviceId> deviceIds,
		ushort nxpVersion,
		ushort scalerVersion,
		byte dscVersion,
		ReadOnlyMemory<byte> rawCapabilities,
		MonitorCapabilities parsedCapabilities,
		MonitorDeviceFeatures features,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base
	(
		new LgDisplayDataChannelWithRetry(compositeI2cBus, true, I2CRetryCount),
		monitorFeatureSetBuilder,
		rawCapabilities,
		parsedCapabilities,
		deviceIds[0],
		friendlyName,
		configurationKey
	)
	{
		CompositeI2cBus = compositeI2cBus;
		_deviceIds = deviceIds;
		_nxpVersion = nxpVersion;
		_scalerVersion = scalerVersion;
		_dscVersion = dscVersion;
		_lgMonitorFeatures = FeatureSet.Create<
			ILgMonitorDeviceFeature,
			LgMonitorDriver,
			ILgMonitorScalerVersionFeature,
			ILgMonitorNxpVersionFeature,
			ILgMonitorDisplayStreamCompressionVersionFeature>(this);
		if ((features & MonitorDeviceFeatures.Lighting) != 0)
		{
			UpdateFeatures(ultraGearLightingFeatures);
		}
		else
		{
			_lightingFeatures = FeatureSet.Empty<ILightingDeviceFeature>();
			_featureSets =
			[
				FeatureSetDescription.CreateStatic<IGenericDeviceFeature>(),
				FeatureSetDescription.CreateStatic<IMonitorDeviceFeature>(),
				FeatureSetDescription.CreateStatic<ILgMonitorDeviceFeature>(),
			];
		}
	}

	protected sealed override IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures(DeviceConfigurationKey configurationKey)
		=> FeatureSet.Create<IGenericDeviceFeature, LgMonitorDriver, IDeviceSerialNumberFeature, IDeviceIdFeature, IDeviceIdsFeature, IVariableFeatureSetDeviceFeature>(this);

	// The firmware archive explicitly names the SV, DV and NV as "Scaler", "DSC" and "NXP"
	// DSC versions are expanded in a single byte, seemingly in BCD format. So, 0x44 for version 44, and 0x51 for version 51.
	public SimpleVersion FirmwareNxpVersion => new((byte)(_nxpVersion >>> 8), (byte)_nxpVersion);
	public SimpleVersion FirmwareScalerVersion => new((byte)(_scalerVersion >>> 8), (byte)_scalerVersion);
	public SimpleVersion FirmwareDisplayStreamCompressionVersion => new((byte)((_dscVersion >>> 4) * 10 | _dscVersion & 0x0F), 0);

	IDeviceFeatureSet<ILgMonitorDeviceFeature> IDeviceDriver<ILgMonitorDeviceFeature>.Features => _lgMonitorFeatures;
	IDeviceFeatureSet<ILightingDeviceFeature> IDeviceDriver<ILightingDeviceFeature>.Features => Volatile.Read(ref _lightingFeatures);

	ImmutableArray<DeviceId> IDeviceIdsFeature.DeviceIds => _deviceIds;
	int? IDeviceIdsFeature.MainDeviceIdIndex => 0;

	public override async ValueTask DisposeAsync()
	{
		try
		{
			await base.DisposeAsync().ConfigureAwait(false);
		}
		finally
		{
			DriversBySerialNumber.TryRemove(new(ConfigurationKey.UniqueId!, this));
		}
	}
}
