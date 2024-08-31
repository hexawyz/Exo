using System.Collections.ObjectModel;
using Exo.Settings.Ui.Services;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ProgrammingViewModel : BindableObject, IConnectedState, IAsyncDisposable
{
	private readonly SettingsServiceConnectionManager _connectionManager;
	private ReadOnlyCollection<ModuleViewModel> _modules;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly IDisposable _stateRegistration;

	public ProgrammingViewModel(SettingsServiceConnectionManager connectionManager)
	{
		_connectionManager = connectionManager;
		_modules = ReadOnlyCollection<ModuleViewModel>.Empty;
		_cancellationTokenSource = new();
		_stateRegistration = _connectionManager.RegisterStateAsync(this).GetAwaiter().GetResult();
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Dispose();
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	async Task IConnectedState.RunAsync(CancellationToken cancellationToken)
	{
		if (_cancellationTokenSource.IsCancellationRequested) return;
		using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, cancellationToken))
		{
			var changeWatchTask = WatchChangesAsync(cts.Token);

			await changeWatchTask;
		}
	}

	void IConnectedState.Reset()
	{
		Modules = ReadOnlyCollection<ModuleViewModel>.Empty;
	}

	private async Task WatchChangesAsync(CancellationToken cancellationToken)
	{
		var programmingService = await _connectionManager.GetProgrammingServiceAsync(cancellationToken);
		var modules = (await programmingService.GetModulesAsync(cancellationToken)).Select(m => new ModuleViewModel(m)).ToArray();

		Modules = Array.AsReadOnly(modules);

		// TODO: Watch custom types & rest of the model.
	}

	public ReadOnlyCollection<ModuleViewModel> Modules
	{
		get => _modules;
		private set => SetValue(ref _modules, value);
	}
}
