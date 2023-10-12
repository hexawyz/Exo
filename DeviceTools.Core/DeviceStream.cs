using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools;

public class DeviceStream : FileStream
{
	public DeviceStream(SafeFileHandle handle, FileAccess access) : base(handle, access)
	{
	}

	public DeviceStream(string path, FileMode mode) : base(path, mode)
	{
	}

#if NET6_0_OR_GREATER
	public DeviceStream(string path, FileStreamOptions options) : base(path, options)
	{
	}
#endif

	public DeviceStream(SafeFileHandle handle, FileAccess access, int bufferSize) : base(handle, access, bufferSize)
	{
	}

	public DeviceStream(string path, FileMode mode, FileAccess access) : base(path, mode, access)
	{
	}

	public DeviceStream(SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync) : base(handle, access, bufferSize, isAsync)
	{
	}

	public DeviceStream(string path, FileMode mode, FileAccess access, FileShare share) : base(path, mode, access, share)
	{
	}

	public DeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize) : base(path, mode, access, share, bufferSize)
	{
	}

	public DeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync) : base(path, mode, access, share, bufferSize, useAsync)
	{
	}

	public DeviceStream(string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options) : base(path, mode, access, share, bufferSize, options)
	{
	}

	public int IoControl(SafeFileHandle deviceFileHandle, int ioControlCode, ReadOnlySpan<byte> inputBuffer, Span<byte> outputBuffer)
		=> unchecked((int)SafeFileHandle.IoControl(unchecked((uint)ioControlCode), inputBuffer, outputBuffer));

#if NET8_0_OR_GREATER
	public unsafe ValueTask<int> IoControlAsync(int ioControlCode, ReadOnlyMemory<byte> inputBuffer, Memory<byte> outputBuffer, CancellationToken cancellationToken)
	{
		var vts = GetIoControlValueTaskSource();
		int errorCode = 0;
		try
		{
			var overlapped = vts.PrepareForOperation(inputBuffer, outputBuffer);

			uint result = NativeMethods.DeviceIoControl
			(
				SafeFileHandle,
				unchecked((uint)ioControlCode),
				(byte*)vts._inputMemoryHandle.Pointer,
				(uint)inputBuffer.Length,
				(byte*)vts._outputMemoryHandle.Pointer,
				(uint)outputBuffer.Length,
				null,
				overlapped
			);

			if (result == 0)
			{
				errorCode = Marshal.GetLastWin32Error();

				if (errorCode == NativeMethods.ErrorIoPending)
				{
					vts.RegisterForCancellation(cancellationToken);
				}
				else
				{
					vts.Dispose();
					return ValueTask.FromException<int>(Marshal.GetExceptionForHR(NativeMethods.ErrorToHResult(errorCode))!);
				}
			}
		}
		catch
		{
			vts.Dispose();
			throw;
		}

		// Completion handled by callback.
		vts.FinishedScheduling();
		return new ValueTask<int>(vts, vts.Version);
	}

	public unsafe ValueTask IoControlAsync(int ioControlCode, ReadOnlyMemory<byte> inputBuffer, CancellationToken cancellationToken)
	{
		var vts = GetIoControlValueTaskSource();
		int errorCode = 0;
		try
		{
			var overlapped = vts.PrepareForOperation(inputBuffer, default);

			uint result = NativeMethods.DeviceIoControl
			(
				SafeFileHandle,
				unchecked((uint)ioControlCode),
				(byte*)vts._inputMemoryHandle.Pointer,
				(uint)inputBuffer.Length,
				(byte*)vts._outputMemoryHandle.Pointer,
				(uint)0,
				null,
				overlapped
			);

			if (result == 0)
			{
				errorCode = Marshal.GetLastWin32Error();

				if (errorCode == NativeMethods.ErrorIoPending)
				{
					vts.RegisterForCancellation(cancellationToken);
				}
				else
				{
					vts.Dispose();
					return ValueTask.FromException(Marshal.GetExceptionForHR(NativeMethods.ErrorToHResult(errorCode))!);
				}
			}
		}
		catch
		{
			vts.Dispose();
			throw;
		}

		// Completion handled by callback.
		vts.FinishedScheduling();
		return new ValueTask(vts, vts.Version);
	}

	public unsafe ValueTask<int> IoControlAsync(int ioControlCode, Memory<byte> outputBuffer, CancellationToken cancellationToken)
		=> IoControlAsync(ioControlCode, default, outputBuffer, cancellationToken);

	private IoControlValueTaskSource? _reusableIoControlValueTaskSource;

	private IoControlValueTaskSource GetIoControlValueTaskSource()
		=> Interlocked.Exchange(ref _reusableIoControlValueTaskSource, null) ?? new IoControlValueTaskSource(this);

	private bool TryToReuse(IoControlValueTaskSource source)
		=> Interlocked.CompareExchange(ref _reusableIoControlValueTaskSource, source, null) is null;

	// This code is heavily adapted from SafeFileHandle.OverlappedValueTaskSource from .NET 8.0.
	// Feel free to refer to the original source for comments.
	// The main difference here is that we have two buffers to manage.
	private sealed unsafe class IoControlValueTaskSource : IValueTaskSource<int>, IValueTaskSource
	{
		internal static readonly IOCompletionCallback IoCallback = (uint errorCode, uint numBytes, NativeOverlapped* overlapped) =>
		{
			var vts = (IoControlValueTaskSource?)ThreadPoolBoundHandle.GetNativeOverlappedState(overlapped)!;

			if (Interlocked.Exchange(ref vts._result, (1ul << 63) | ((ulong)numBytes << 32) | errorCode) != 0)
			{
				vts.Complete(errorCode, numBytes);
			}
		};

		internal readonly PreAllocatedOverlapped _preallocatedOverlapped;
		internal readonly DeviceStream _stream;
		internal MemoryHandle _inputMemoryHandle;
		internal MemoryHandle _outputMemoryHandle;
		internal ManualResetValueTaskSourceCore<int> _source;
		private NativeOverlapped* _overlapped;
		private CancellationTokenRegistration _cancellationRegistration;
		internal ulong _result;

		internal IoControlValueTaskSource(DeviceStream stream)
		{
			_stream = stream;
			_source.RunContinuationsAsynchronously = true;
			_preallocatedOverlapped = PreAllocatedOverlapped.UnsafeCreate(IoCallback, this, null);
		}

		internal void Dispose()
		{
			ReleaseResources();
			_preallocatedOverlapped.Dispose();
		}

		internal NativeOverlapped* PrepareForOperation(ReadOnlyMemory<byte> inputBuffer, Memory<byte> outputBuffer)
		{
			_result = 0;
			_inputMemoryHandle = inputBuffer.Pin();
			_outputMemoryHandle = outputBuffer.Pin();
			_overlapped = SafeFileHandleExtensions.GetThreadPoolBinding(_stream.SafeFileHandle).AllocateNativeOverlapped(_preallocatedOverlapped);
			return _overlapped;
		}

		internal void RegisterForCancellation(CancellationToken cancellationToken)
		{
			if (cancellationToken.CanBeCanceled)
			{
				try
				{
					_cancellationRegistration = cancellationToken.UnsafeRegister(static (s, token) =>
					{
						var vts = (IoControlValueTaskSource)s!;
						if (!vts._stream.SafeFileHandle.IsInvalid)
						{
							try
							{
								NativeMethods.CancelIoEx(vts._stream.SafeFileHandle, vts._overlapped);
							}
							catch (ObjectDisposedException) { }
						}
					}, this);
				}
				catch (OutOfMemoryException)
				{
				}
			}
		}


		public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
			=> _source.OnCompleted(continuation, state, token, flags);

		void IValueTaskSource.GetResult(short token) => _ = GetResult(token);

		public int GetResult(short token)
		{
			try
			{
				return _source.GetResult(token);
			}
			finally
			{
				TryToReuse();
			}
		}

		private void TryToReuse()
		{
			_source.Reset();
			if (!_stream.TryToReuse(this))
			{
				Dispose();
			}
		}

		internal short Version => _source.Version;

		private void ReleaseResources()
		{
			_cancellationRegistration.Dispose();

			_inputMemoryHandle.Dispose();
			_outputMemoryHandle.Dispose();

			if (_overlapped != null)
			{
				SafeFileHandleExtensions.GetThreadPoolBinding(_stream.SafeFileHandle).FreeNativeOverlapped(_overlapped);
				_overlapped = null;
			}
		}

		internal void FinishedScheduling()
		{
			ulong result = Interlocked.Exchange(ref _result, 1);
			if (result != 0)
			{
				Complete(errorCode: (uint)result, numBytes: (uint)(result >> 32) & 0x7FFFFFFF);
			}
		}

		internal void Complete(uint errorCode, uint numBytes)
		{
			ReleaseResources();

			switch (errorCode)
			{
			case 0:
			case NativeMethods.ErrorBrokenPipe:
			case NativeMethods.ErrorNoData:
			case NativeMethods.ErrorHandleEndOfFile:
				_source.SetResult((int)numBytes);
				break;

			case NativeMethods.ErrorOperationAborted:
				CancellationToken ct = _cancellationRegistration.Token;
				_source.SetException(ct.IsCancellationRequested ? new OperationCanceledException(ct) : new OperationCanceledException());
				break;

			default:
				_source.SetException(Marshal.GetExceptionForHR(NativeMethods.ErrorToHResult((int)errorCode))!);
				break;
			}
		}
	}
#endif
}
