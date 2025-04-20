using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ConnectionViewModel : BindableObject
{
	private ConnectionStatus _connectionStatus;

	public ConnectionStatus ConnectionStatus => _connectionStatus;

	internal void OnConnectionStatusChanged(ConnectionStatus connectionStatus)
	{
		_connectionStatus = connectionStatus;
		NotifyPropertyChanged(ChangedProperty.ConnectionStatus);
	}
}
