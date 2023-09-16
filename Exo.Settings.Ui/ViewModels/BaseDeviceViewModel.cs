using System;
using Exo.Ui;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class BaseDeviceViewModel : BindableObject
{
	public BaseDeviceViewModel(DeviceInformation deviceInformation)
	{
		Id = deviceInformation.Id;
		_friendlyName = deviceInformation.FriendlyName;
		_category = deviceInformation.Category;
		_isAvailable = deviceInformation.IsAvailable;
	}

	public Guid Id { get; }

	private string _friendlyName;
	public string FriendlyName
	{
		get => _friendlyName;
		set => SetValue(ref _friendlyName, value);
	}

	private DeviceCategory _category;
	public DeviceCategory Category
	{
		get => _category;
		set => SetValue(ref _category, value);
	}

	private bool _isAvailable;
	public bool IsAvailable
	{
		get => _isAvailable;
		set => SetValue(ref _isAvailable, value);
	}
}
