using System.Threading.Channels;
using Exo.Programming;

namespace Exo.Service;

public sealed class ProgrammingService : IAsyncDisposable
{
	private readonly ChannelReader<Event> _eventReader;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Task _runTask;

	public ProgrammingService(ChannelReader<Event> eventReader)
	{
		_eventReader = eventReader;
		_cancellationTokenSource = new();
		_runTask = RunAsync();
	}

	public async ValueTask DisposeAsync()
	{
		if (_runTask.IsCompleted) return;

		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		await _runTask.ConfigureAwait(false);
	}

	private async Task RunAsync()
	{
		await foreach (var @event in _eventReader.ReadAllAsync().ConfigureAwait(false))
		{
		}
	}

	public void RegisterModule<T>()
	{
	}

	public async IAsyncEnumerable<ModuleDefinition> GetModules()
	{
		yield break;
	}
}
