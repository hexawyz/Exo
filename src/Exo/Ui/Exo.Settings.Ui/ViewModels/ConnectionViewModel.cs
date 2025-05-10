using Exo.Settings.Ui.Services;
using Exo.Ui;
using WinRT;

namespace Exo.Settings.Ui.ViewModels;

[GeneratedBindableCustomProperty]
internal sealed partial class ConnectionViewModel : BindableObject
{
	private ConnectionStatus _connectionStatus;

	public ConnectionStatus ConnectionStatus => _connectionStatus;

	internal void OnConnectionStatusChanged(ConnectionStatus connectionStatus)
	{
		_connectionStatus = connectionStatus;
		NotifyPropertyChanged(ChangedProperty.ConnectionStatus);
	}
}
