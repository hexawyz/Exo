using Exo.Ui.Contracts;
using ProtoBuf.Grpc.Client;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class SettingsViewModel : BindableObject
{
	private readonly ServiceConnectionManager _connectionManager;
	private readonly DevicesViewModel _devicesViewModel;

	public SettingsViewModel()
	{
		_connectionManager = new("Local\\Exo.Service.Configuration");
		_devicesViewModel = new(_connectionManager.Channel.CreateGrpcService<IDeviceService>());
	}

	public DevicesViewModel Devices => _devicesViewModel;
}
