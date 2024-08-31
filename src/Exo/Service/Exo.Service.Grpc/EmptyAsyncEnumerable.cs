namespace Exo.Service.Grpc;

internal sealed class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
{
	public static readonly EmptyAsyncEnumerable<T> Instance = new();

	private EmptyAsyncEnumerable() { }

	public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => EmptyAsyncEnumerator.Instance;

	private sealed class EmptyAsyncEnumerator : IAsyncEnumerator<T>
	{
		public static readonly EmptyAsyncEnumerator Instance = new();

		private EmptyAsyncEnumerator() { }

		public T Current => default!;
		public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(false);
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
