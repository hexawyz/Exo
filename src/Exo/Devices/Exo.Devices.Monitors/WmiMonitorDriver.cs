using System.Collections.Immutable;
using System.Reactive.Linq;
using DeviceTools;
using DeviceTools.DisplayDevices;
using DeviceTools.DisplayDevices.Configuration;
using Exo.Discovery;
using Exo.Features;
using Exo.Features.Monitors;
using Exo.Monitors;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;

namespace Exo.Devices.Monitors;

public partial class WmiMonitorDriver
	: GenericMonitorDriver,
	IDeviceDriver<IGenericDeviceFeature>,
	IDeviceDriver<IMonitorDeviceFeature>,
	IDeviceIdFeature,
	IDeviceSerialNumberFeature,
	IMonitorBrightnessFeature
{
	private const string CimNamespace = @"ROOT/wmi";
	private const string BrightnessClassName = "WmiMonitorBrightness";
	private const string BrightnessMethodsClassName = "WmiMonitorBrightnessMethods";
	private const string InstanceNamePropertyName = "InstanceName";
	private const string CurrentBrightnessPropertyName = "CurrentBrightness";
	private const string LevelPropertyName = "Level";
	private const string WmiSetBrightnessMethodName = "WmiSetBrightness";

	private sealed class MonitorFeatureSetBuilder : IMonitorFeatureSetBuilder
	{
		private IDeviceFeatureSet<IMonitorDeviceFeature> CreateFeatureSet(WmiMonitorDriver driver)
			=> FeatureSet.Create<IMonitorDeviceFeature, IMonitorBrightnessFeature>(driver);

		IDeviceFeatureSet<IMonitorDeviceFeature> IMonitorFeatureSetBuilder.CreateFeatureSet(GenericMonitorDriver driver)
			=> CreateFeatureSet((WmiMonitorDriver)driver);
	}

	internal static async ValueTask<DriverCreationResult<SystemDevicePath>?> CreateAsync
	(
		ILogger<MccsMonitorDriver> logger,
		ImmutableArray<SystemDevicePath> keys,
		string friendlyName,
		DeviceId deviceId,
		MonitorId monitorId,
		Edid edid,
		string topLevelDeviceName,
		CancellationToken cancellationToken
	)
	{
		if (TryGetMonitorDefinition(monitorId, out var definition))
		{
			if (definition.Name is not null) friendlyName = definition.Name;
		}

		var cimSession = await CimSession.CreateAsync(null);
		// TODO: Investigate the exact meaning of this suffix.
		// Is it always _0?
		// Are there cases wehre there could be more than one instance for the same device?
		// If that were the case, what would be the meaning?
		string lookupKey = topLevelDeviceName + "_0";
		var brightnessLookupKey = new CimInstance(BrightnessClassName, CimNamespace)
		{
			CimInstanceProperties =
			{
				CimProperty.Create(InstanceNamePropertyName, lookupKey, CimFlags.Property | CimFlags.Key)
			}
		};
		CimInstance brightnessInstance;
		CimInstance brightnessMethodsInstance;
		try
		{
			brightnessInstance = await cimSession.GetInstanceAsync(CimNamespace, brightnessLookupKey);
			brightnessMethodsInstance = await cimSession.GetInstanceAsync
			(
				CimNamespace,
				new CimInstance(BrightnessMethodsClassName, CimNamespace)
				{
					CimInstanceProperties =
					{
						CimProperty.Create(InstanceNamePropertyName, lookupKey, CimFlags.Property | CimFlags.Key)
					}
				}
			);
		}
		catch
		{
			cimSession.Dispose();
			throw;
		}

		if (friendlyName is null && edid.ProductName is not null) friendlyName = edid.ProductName;

		if (friendlyName is null) throw new InvalidOperationException("No friendly name for the monitor.");

		return new DriverCreationResult<SystemDevicePath>
		(
			keys,
			new WmiMonitorDriver
			(
				new MonitorFeatureSetBuilder(),
				cimSession,
				brightnessLookupKey,
				brightnessMethodsInstance,
				deviceId,
				friendlyName,
				new("monitor", topLevelDeviceName, deviceId.ToString(), edid.SerialNumber)
			)
		);
	}

	private readonly CimSession _cimSession;
	private readonly CimInstance _brightnessLookupKey;
	private readonly CimInstance _brightnessMethodsInstance;

	public WmiMonitorDriver
	(
		IMonitorFeatureSetBuilder featureSetBuilder,
		CimSession cimSession,
		CimInstance brightnessLookupKey,
		CimInstance brightnessMethodsInstance,
		DeviceId deviceId,
		string friendlyName,
		DeviceConfigurationKey configurationKey
	) : base(featureSetBuilder, deviceId, friendlyName, configurationKey)
	{
		_cimSession = cimSession;
		_brightnessLookupKey = brightnessLookupKey;
		_brightnessMethodsInstance = brightnessMethodsInstance;
	}

	public override ValueTask DisposeAsync()
	{
		_brightnessLookupKey.Dispose();
		_brightnessMethodsInstance.Dispose();
		_cimSession.Dispose();
		return ValueTask.CompletedTask;
	}

	async ValueTask<ContinuousValue> IContinuousVcpFeature.GetValueAsync(CancellationToken cancellationToken)
	{
		using var brightnessInstance = await _cimSession.GetInstanceAsync(CimNamespace, _brightnessLookupKey);
		var value = (byte)brightnessInstance.CimInstanceProperties[CurrentBrightnessPropertyName].Value;
		var levels = (byte[])brightnessInstance.CimInstanceProperties[LevelPropertyName].Value;
		return new(value, levels[0], levels[^1]);
	}

	async ValueTask IContinuousVcpFeature.SetValueAsync(ushort value, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
		await _cimSession.InvokeMethodAsync
		(
			CimNamespace,
			_brightnessMethodsInstance,
			WmiSetBrightnessMethodName,
			new CimMethodParametersCollection
			{
				CimMethodParameter.Create("Timeout", 10, CimType.UInt32, CimFlags.In),
				CimMethodParameter.Create("Brightness", (byte)value, CimType.UInt8, CimFlags.In),
			}
		);
	}
}
