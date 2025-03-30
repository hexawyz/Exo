namespace Exo.Settings.Ui.Ipc;

internal sealed class EmptyAsyncEnumerable<TValue> : IAsyncEnumerable<TValue>
{
	public static readonly EmptyAsyncEnumerable<TValue> Instance = new();

	private sealed class Enumerator : IAsyncEnumerator<TValue>
	{
		public static readonly Enumerator Instance = new();

		public ValueTask<bool> MoveNextAsync() => new(false);

		public TValue Current => default!;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default) => Enumerator.Instance;
}
