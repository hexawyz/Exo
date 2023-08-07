using Exo.Ui.Contracts;
using ProtoBuf.Grpc.Client;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject
{
	private readonly ServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly LightingViewModel _lightingViewModel;
	private string _title;

	public SettingsViewModel()
	{
		_connectionManager = new("Local\\Exo.Service.Configuration");
		_devicesViewModel = new(_connectionManager.Channel.CreateGrpcService<IDeviceService>());
		_lightingViewModel = new(_connectionManager.Channel.CreateGrpcService<ILightingService>());
	}

	public DevicesViewModel Devices => _devicesViewModel;
	public LightingViewModel Lighting => _lightingViewModel;

	public string Title
	{
		get => _title;
		set => SetValue(ref _title, value);
	}
}
