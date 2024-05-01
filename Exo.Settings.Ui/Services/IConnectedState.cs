namespace Exo.Settings.Ui.Services;

internal interface IConnectedState
{
	Task RunAsync(CancellationToken cancellationToken);
	void Reset();
}
