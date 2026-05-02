using System.Collections.Immutable;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using Exo.Discovery;
using Exo.Features;
using Exo.I2C;
using Microsoft.Extensions.Logging;

namespace Exo.Devices.Monitors;

public abstract partial class GenericMonitorDriver
	: Driver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature
{
	public interface IMonitorFeatureSetBuilder
	{
		public IDeviceFeatureSet<IMonitorDeviceFeature> CreateFeatureSet(GenericMonitorDriver driver);
	}

	[DiscoverySubsystem<MonitorDiscoverySubsystem>]
	[DeviceInterfaceClass(DeviceInterfaceClass.Monitor)]
	public static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<MccsMonitorDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		DeviceId deviceId,
		Edid edid,
		II2cBus i2cBus,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		var monitorId = new MonitorId(edid.VendorId, edid.ProductId);
		return await MccsMonitorDriver.CreateAsync
		(
			logger,
			keys,
			friendlyName,
			deviceId,
			monitorId,
			edid,
			i2cBus,
			topLevelDeviceName,
			cancellationToken
		).ConfigureAwait(false);
	}

	public override DeviceCategory DeviceCategory => DeviceCategory.Monitor;

	private readonly DeviceId _deviceId;

	private readonly IDeviceFeatureSet<IGenericDeviceFeature> _genericFeatures;
	private readonly IDeviceFeatureSet<IMonitorDeviceFeature> _monitorFeatures;

	protected IDeviceFeatureSet<IGenericDeviceFeature> GenericFeatures => _genericFeatures;
	protected IDeviceFeatureSet<IMonitorDeviceFeature> MonitorFeatures => _monitorFeatures;

	IDeviceFeatureSet<IGenericDeviceFeature> IDeviceDriver<IGenericDeviceFeature>.Features => _genericFeatures;
	IDeviceFeatureSet<IMonitorDeviceFeature> IDeviceDriver<IMonitorDeviceFeature>.Features => _monitorFeatures;

	DeviceId IDeviceIdFeature.DeviceId => _deviceId;

	string IDeviceSerialNumberFeature.SerialNumber => ConfigurationKey.UniqueId!;

	protected GenericMonitorDriver
	(
		IMonitorFeatureSetBuilder featureSetBuilder,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	)
		: base(friendlyName, configurationKey)
	{
		_deviceId = deviceId;

		_genericFeatures = CreateGenericFeatures(configurationKey);

		_monitorFeatures = CreateMonitorFeatures(featureSetBuilder);
	}

	protected virtual IDeviceFeatureSet<IGenericDeviceFeature> CreateGenericFeatures(DeviceConfigurationKey configurationKey)
		=> configurationKey.UniqueId is not null ?
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature, IDeviceSerialNumberFeature>(this) :
			FeatureSet.Create<IGenericDeviceFeature, GenericMonitorDriver, IDeviceIdFeature>(this);

	protected virtual IDeviceFeatureSet<IMonitorDeviceFeature> CreateMonitorFeatures(IMonitorFeatureSetBuilder featureSetBuilder)
		=> featureSetBuilder.CreateFeatureSet(this);
}
