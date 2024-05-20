namespace Exo;

public static class CancellationTokenExtensions
{
	public static CancellationTokenRegistration UnsafeRegister(this CancellationToken cancellationToken, TaskCompletionSource taskCompletionSource)
		=> cancellationToken.UnsafeRegister(static (state, ct) => ((TaskCompletionSource)state!).TrySetCanceled(ct), taskCompletionSource);

	public static CancellationTokenRegistration UnsafeRegister<T>(this CancellationToken cancellationToken, TaskCompletionSource<T> taskCompletionSource)
		=> cancellationToken.UnsafeRegister(static (state, ct) => ((TaskCompletionSource<T>)state!).TrySetCanceled(ct), taskCompletionSource);
}
