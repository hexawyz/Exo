using Exo.Ui;
using Exo.Ui.Contracts;
using ProtoBuf.Grpc.Client;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject
{
	private readonly ServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;
	private readonly LightingViewModel _lightingViewModel;
	private readonly ProgrammingViewModel _programmingViewModel;
	private string? _icon;
	private string _title;

	public SettingsViewModel()
	{
		_connectionManager = new("Local\\Exo.Service.Configuration");
		_devicesViewModel = new
		(
			_connectionManager.Channel.CreateGrpcService<IDeviceService>(),
			_connectionManager.Channel.CreateGrpcService<IMouseService>(),
			_connectionManager.Channel.CreateGrpcService<IMonitorService>()
		);
		_lightingViewModel = new(_connectionManager.Channel.CreateGrpcService<ILightingService>(), _devicesViewModel);
		_programmingViewModel = new(_connectionManager.Channel.CreateGrpcService<IProgrammingService>());
		_icon = string.Empty;
		_title = string.Empty;
	}

	public DevicesViewModel Devices => _devicesViewModel;
	public LightingViewModel Lighting => _lightingViewModel;
	public ProgrammingViewModel Programming => _programmingViewModel;

	public string? Icon
	{
		get => _icon;
		set => SetValue(ref _icon, value);
	}

	public string Title
	{
		get => _title;
		set => SetValue(ref _title, value);
	}
}
