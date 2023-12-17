using System.Collections.ObjectModel;
using Exo.Ui;
using Exo.Ui.Contracts;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ProgrammingViewModel : BindableObject, IAsyncDisposable
{
	private readonly IProgrammingService _programmingService;
	private ReadOnlyCollection<ModuleViewModel> _modules;

	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _changeWatchTask;

	public ProgrammingViewModel(IProgrammingService programmingService)
	{
		_programmingService = programmingService;
		_modules = ReadOnlyCollection<ModuleViewModel>.Empty;
		_cancellationTokenSource = new();
		_changeWatchTask = WatchChangesAsync(_cancellationTokenSource.Token);
	}

	public async ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Dispose();
		await _changeWatchTask;
	}

	private async Task WatchChangesAsync(CancellationToken cancellationToken)
	{
		var modules = (await _programmingService.GetModulesAsync(cancellationToken)).Select(m => new ModuleViewModel(m)).ToArray();

		Modules = Array.AsReadOnly(modules);

		// TODO: Watch custom types & rest of the model.
	}

	public ReadOnlyCollection<ModuleViewModel> Modules
	{
		get => _modules;
		private set => SetValue(ref _modules, value);
	}
}
