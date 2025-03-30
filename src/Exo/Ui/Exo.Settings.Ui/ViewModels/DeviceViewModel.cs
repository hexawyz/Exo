using System.Collections.Immutable;
using System.Collections.ObjectModel;
using DeviceTools;
using Exo.Contracts.Ui.Settings;
using Exo.Service;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BindableObject, IDisposable
{
	public DeviceViewModel
	(
		SettingsServiceConnectionManager connectionManager,
		ReadOnlyObservableCollection<ImageViewModel> availableImages,
		ISettingsMetadataService metadataService,
		INotificationSystem notificationSystem,
		IPowerService powerService,
		IMouseService mouseService,
		IEmbeddedMonitorService embeddedMonitorService,
		ILightService lightService,
		IRasterizationScaleProvider rasterizationScaleProvider,
		DeviceStateInformation deviceInformation
	)
	{
		Id = deviceInformation.Id;
		_friendlyName = deviceInformation.FriendlyName;
		_category = deviceInformation.Category;
		_isAvailable = deviceInformation.IsAvailable;
		_rawDeviceIds = deviceInformation.DeviceIds.IsDefaultOrEmpty ? [] : deviceInformation.DeviceIds;
		_mainDeviceIdIndex = deviceInformation.MainDeviceIdIndex;
		_deviceIds = GenerateDeviceIds(_rawDeviceIds, _mainDeviceIdIndex);
		_serialNumber = deviceInformation.SerialNumber;

		if (deviceInformation.FeatureIds is not null)
		{
			foreach (var featureId in deviceInformation.FeatureIds)
			{
				if (featureId == WellKnownGuids.PowerDeviceFeature)
				{
					PowerFeatures ??= new(this, powerService);
				}
				else if (featureId == WellKnownGuids.MouseDeviceFeature)
				{
					MouseFeatures ??= new(this, mouseService);
				}
				else if (featureId == WellKnownGuids.MonitorDeviceFeature)
				{
					MonitorFeatures ??= new(this, metadataService, connectionManager);
				}
				else if (featureId == WellKnownGuids.LightDeviceFeature)
				{
					LightFeatures ??= new(this, metadataService, lightService, notificationSystem);
				}
				else if (featureId == WellKnownGuids.EmbeddedMonitorDeviceFeature)
				{
					EmbeddedMonitorFeatures ??= new(this, availableImages, rasterizationScaleProvider, metadataService, embeddedMonitorService, notificationSystem);
				}
			}
		}
	}

	public void Dispose()
	{
		EmbeddedMonitorFeatures?.Dispose();
	}

	public Guid Id { get; }

	private string _friendlyName;
	public string FriendlyName
	{
		get => _friendlyName;
		set => SetValue(ref _friendlyName, value, ChangedProperty.FriendlyName);
	}

	private DeviceCategory _category;
	public DeviceCategory Category
	{
		get => _category;
		set => SetValue(ref _category, value, ChangedProperty.Category);
	}

	private ImmutableArray<DeviceId> _rawDeviceIds;
	private int? _mainDeviceIdIndex;
	private ReadOnlyCollection<DeviceIdViewModel> _deviceIds;

	//private readonly ExtendedDeviceInformation _extendedDeviceInformation;

	public ReadOnlyCollection<DeviceIdViewModel> DeviceIds
	{
		get => _deviceIds;
		set => SetValue(ref _deviceIds, value, ChangedProperty.DeviceIds);
	}

	public DeviceIdViewModel? MainDeviceId
		=> DeviceIds.Count > 0 ?
			DeviceIds[_mainDeviceIdIndex.GetValueOrDefault()] :
			null;

	public void UpdateDeviceIds(ImmutableArray<DeviceId> deviceIds, int? mainDeviceIdIndex)
	{
		if (!_rawDeviceIds.SequenceEqual(deviceIds) || _mainDeviceIdIndex != mainDeviceIdIndex)
		{
			_rawDeviceIds = deviceIds;
			_mainDeviceIdIndex = mainDeviceIdIndex;
			DeviceIds = GenerateDeviceIds(deviceIds, mainDeviceIdIndex);
		}
	}

	private static ReadOnlyCollection<DeviceIdViewModel> GenerateDeviceIds(ImmutableArray<DeviceId> deviceIds, int? mainDeviceIdIndex)
	{
		if (deviceIds.IsDefaultOrEmpty) return ReadOnlyCollection<DeviceIdViewModel>.Empty;

		var vms = new DeviceIdViewModel[deviceIds.Length];

		for (int i = 0; i < vms.Length; i++)
		{
			vms[i] = new(deviceIds[i], mainDeviceIdIndex is not null && i == mainDeviceIdIndex.GetValueOrDefault());
		}

		return Array.AsReadOnly(vms);
	}

	private bool _isAvailable;
	public bool IsAvailable
	{
		get => _isAvailable;
		set
		{
			if (SetValue(ref _isAvailable, value, ChangedProperty.IsAvailable) && !value)
			{
				LightFeatures?.OnDeviceOffline();
			}
		}
	}

	private string? _serialNumber;
	public string? SerialNumber
	{
		get => _serialNumber;
		set => SetValue(ref _serialNumber, value, ChangedProperty.SerialNumber);
	}

	// If the device has battery or other power management features, this hosts the power-related features.
	public PowerFeaturesViewModel? PowerFeatures { get; }

	// If the device is a mouse, this hosts the mouse-related features.
	public MouseDeviceFeaturesViewModel? MouseFeatures { get; }

	// If the device is a monitor, this hosts the monitor-related features.
	public MonitorDeviceFeaturesViewModel? MonitorFeatures { get; }

	// If the device has embedded monitors, this hosts the embedded monitor-related features.
	public EmbeddedMonitorFeaturesViewModel? EmbeddedMonitorFeatures { get; }

	// If the device has embedded monitors, this hosts the light-related features.
	public LightDeviceFeaturesViewModel? LightFeatures { get; }
}
