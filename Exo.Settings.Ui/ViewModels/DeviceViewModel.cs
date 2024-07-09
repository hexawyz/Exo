using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Exo.Contracts.Ui.Settings;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal class DeviceViewModel : BindableObject
{
	public DeviceViewModel(SettingsServiceConnectionManager connectionManager, ISettingsMetadataService metadataService, DeviceInformation deviceInformation)
	{
		Id = deviceInformation.Id;
		_friendlyName = deviceInformation.FriendlyName;
		_category = deviceInformation.Category;
		_isAvailable = deviceInformation.IsAvailable;
		_rawDeviceIds = deviceInformation.DeviceIds.IsDefaultOrEmpty ? [] : deviceInformation.DeviceIds;
		_mainDeviceIdIndex = deviceInformation.MainDeviceIdIndex;
		_deviceIds = GenerateDeviceIds(_rawDeviceIds, _mainDeviceIdIndex);
		_serialNumber = deviceInformation.SerialNumber;

		//_extendedDeviceInformation = extendedDeviceInformation;
		if (deviceInformation.FeatureIds is not null)
		{
			if (deviceInformation.FeatureIds.Contains(WellKnownGuids.MouseDeviceFeature))
			{
				MouseFeatures = new(this);
			}
			if (deviceInformation.FeatureIds.Contains(WellKnownGuids.MonitorDeviceFeature))
			{
				MonitorFeatures = new(this, metadataService, connectionManager);
			}
		}
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
		set => SetValue(ref _isAvailable, value, ChangedProperty.IsAvailable);
	}

	private string? _serialNumber;
	public string? SerialNumber
	{
		get => _serialNumber;
		set => SetValue(ref _serialNumber, value, ChangedProperty.SerialNumber);
	}

	private BatteryStateViewModel? _batteryState;
	public BatteryStateViewModel? BatteryState
	{
		get => _batteryState;
		set => SetValue(ref _batteryState, value, ChangedProperty.BatteryState);
	}

	// If the device is a mouse, this hosts the mouse-related features.
	public MouseDeviceFeaturesViewModel? MouseFeatures { get; }

	// If the device is a monitor, this hosts the mouse-related features.
	public MonitorDeviceFeaturesViewModel? MonitorFeatures { get; }
}
