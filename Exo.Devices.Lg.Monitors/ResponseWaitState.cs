using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Exo.Devices.Lg.Monitors;

internal abstract class ResponseWaitState
{
	private TaskCompletionSource _taskCompletionSource = new();

	public TaskCompletionSource TaskCompletionSource => Volatile.Read(ref _taskCompletionSource);

	public abstract void OnDataReceived(ReadOnlySpan<byte> message);

	[DebuggerHidden]
	[DebuggerStepThrough]
	[StackTraceHidden]
	public void SetNewException(Exception exception) => SetException(ExceptionDispatchInfo.SetCurrentStackTrace(exception));

	private void SetException(Exception exception) => TaskCompletionSource.TrySetException(exception);

	public void Reset()
	{
		TaskCompletionSource.TrySetCanceled();
		Volatile.Write(ref _taskCompletionSource, new());
	}
}
