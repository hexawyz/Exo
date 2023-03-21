using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DeviceTools.Logitech.HidPlusPlus;

// This base class allows working with the various return type of Send operations without needing to know the expected type of result when it is not necessary.
internal abstract class PendingOperation
{
	// The type of task completion source will indicate the type of response message expected.
	protected object TaskCompletionSource { get; }

	private readonly RawMessageHeader _header;
	public ref readonly RawMessageHeader Header => ref _header;

	public long Timestamp { get; }

	protected PendingOperation(RawMessageHeader header, object taskCompletionSource)
	{
		_header = header;
		TaskCompletionSource = taskCompletionSource;
		Timestamp = Stopwatch.GetTimestamp();
	}

	// Allows to await the Task exposed by the TaskCompletionSource object.
	// We may use this method to serialize requests in the case of an USB receiver with multiple devices.
	public abstract Task WaitAsync();

	public abstract bool TrySetCanceled();

	// Sets the exception on the TaskCompletionSource object.
	public abstract bool TrySetException(Exception exception);

	public abstract bool TrySetResult(ReadOnlySpan<byte> buffer);
}

internal sealed class EmptyPendingOperation : PendingOperation
{
	private new TaskCompletionSource TaskCompletionSource => Unsafe.As<TaskCompletionSource>(base.TaskCompletionSource);

	public EmptyPendingOperation(RawMessageHeader header)
		: base(header, new TaskCompletionSource())
	{
	}

	public override Task WaitAsync() => TaskCompletionSource.Task;

	public override bool TrySetCanceled() => TaskCompletionSource.TrySetCanceled();

	public override bool TrySetException(Exception exception) => TaskCompletionSource.TrySetException(exception);

	public override bool TrySetResult(ReadOnlySpan<byte> buffer) => TaskCompletionSource.TrySetResult();
}

internal sealed class MessagePendingOperation<T> : PendingOperation
	where T : struct, IMessageParameters
{
	private new TaskCompletionSource<T> TaskCompletionSource => Unsafe.As<TaskCompletionSource<T>>(base.TaskCompletionSource);

	public MessagePendingOperation(RawMessageHeader header)
		: base(header, new TaskCompletionSource<T>())
	{
		ParameterInformation<T>.ThrowIfInvalid();
	}

	public sealed override Task WaitAsync() => TaskCompletionSource.Task;

	public sealed override bool TrySetCanceled() => TaskCompletionSource.TrySetCanceled();

	public sealed override bool TrySetException(Exception exception) => TaskCompletionSource.TrySetException(exception);

	public sealed override bool TrySetResult(ReadOnlySpan<byte> buffer)
	{
		// Hopefully, most of the time, we'll set results of the exact length.
		if (buffer.Length == Unsafe.SizeOf<T>() + 4)
		{
			return TaskCompletionSource.TrySetResult(Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(buffer[4])));
		}

		// But we also allow truncating data that is too large, or returning smaller data at the beginning of a zeroed buffer.
		return TrySetOtherLengthResult(buffer);
	}

	private bool TrySetOtherLengthResult(ReadOnlySpan<byte> buffer)
	{
		// TODO: Rewrite this to support truncation if allowed cases. (Should be declared on the type, e.g. by implementing an interface that says how it can be truncated)

		//// Check that the parameter length is actually supported by the parameter type. If not, this is an implementation mismatch or a bug.
		//// We use the report ID here, but the buffer length could also be an appropriate option.
		//// We do, however, expect the length to be strictly matching the report ID, as this is only handled in HidPlusPlusTransport.
		//bool isLengthSupported = buffer[0] switch
		//{
		//	HidPlusPlusTransport.ShortReportId => ParameterInformation<T>.SupportsShort,
		//	HidPlusPlusTransport.LongReportId => ParameterInformation<T>.SupportsVeryLong,
		//	HidPlusPlusTransport.VeryLongReportId => ParameterInformation<T>.SupportsLong,
		//	_ => false,
		//};

		//// If the length is supported, it means the parameter is at least the length specified. (Validated by ParameterInformation<T>)
		//// We allow receiving smaller parameters but not larger, as it could be an implementation error.
		//// This could cause problem if a specific parameter is later extended by the spec to support longer messages. In that case, the lib should be upgraded to support the new hardware.
		//if (!isLengthSupported)
		//{
		//	return TaskCompletionSource.TrySetException
		//	(
		//		ExceptionDispatchInfo.SetCurrentStackTrace
		//		(
		//			new InvalidOperationException($"Parameter of type {typeof(T)} is incompatible with reports of length {buffer.Length}.")
		//		)
		//	);
		//}

		T parameters = default;

		var dst = ParameterInformation<T>.GetNativeSpan(ref parameters);
		var src = buffer[4..];

		int length = Math.Min(dst.Length, src.Length);

		src[..length].CopyTo(dst);
		dst[length..].Clear();

		return TaskCompletionSource.TrySetResult(parameters);
	}
}
