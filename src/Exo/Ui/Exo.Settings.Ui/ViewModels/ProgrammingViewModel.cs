using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Exo.Programming;
using Exo.Ui;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class ProgrammingViewModel : BindableObject, IAsyncDisposable
{
	private ReadOnlyCollection<ModuleViewModel> _modules;

	private readonly CancellationTokenSource _cancellationTokenSource;

	public ProgrammingViewModel()
	{
		_modules = ReadOnlyCollection<ModuleViewModel>.Empty;
		_cancellationTokenSource = new();
	}

	public ValueTask DisposeAsync()
	{
		_cancellationTokenSource.Cancel();
		return ValueTask.CompletedTask;
	}

	internal void OnConnected()
	{
	}

	internal void Reset()
	{
		Modules = ReadOnlyCollection<ModuleViewModel>.Empty;
	}

	internal void HandleMetadata(ImmutableArray<ModuleDefinition> modules)
	{
		Modules = Array.AsReadOnly(modules.Select(m => new ModuleViewModel(m)).ToArray());
	}

	public ReadOnlyCollection<ModuleViewModel> Modules
	{
		get => _modules;
		private set => SetValue(ref _modules, value);
	}
}
