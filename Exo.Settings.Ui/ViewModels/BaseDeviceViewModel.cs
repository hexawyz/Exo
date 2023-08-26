using System;
using Exo.Ui;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal class BaseDeviceViewModel : BindableObject
{
	private readonly DeviceInformation _deviceInformation;

	public BaseDeviceViewModel(DeviceInformation deviceInformation)
	{
		_deviceInformation = deviceInformation;
	}

	public Guid Id => _deviceInformation.Id;

	public string FriendlyName => _deviceInformation.FriendlyName;

	public DeviceCategory Category => _deviceInformation.Category;
}
