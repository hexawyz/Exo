using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DeviceTools.FilterExpressions;

namespace DeviceTools
{
	public sealed class DeviceQuery
	{
		private static readonly UnboundedChannelOptions EnumerateAllChannelOptions = new UnboundedChannelOptions()
		{
			SingleReader = true,
			// Manual testing with Thread.Sleep seems to indicate that the callback is called in non-concurrent sequence.
			// Peeking at the code using Ghidra seems to confirm this. I could still be wrong, though.
			SingleWriter = true,
			// We don't want to lock the DevQuery threadpool for too long, so we can't have synchronous completions.
			// Also, it could generate deadlocks.
			AllowSynchronousContinuations = false
		};

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
#endif
		private static unsafe void EnumerateAllCallback(IntPtr handle, IntPtr context, NativeMethods.DeviceQueryResultActionData* action)
		{
			var writer = Unsafe.As<ChannelWriter<DeviceObjectInformation>>(GCHandle.FromIntPtr(context).Target)!;
			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
#if NET6_0_OR_GREATER
				writer.TryWrite(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString()));
#else
				writer.TryWrite(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!));
#endif
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
					writer.TryComplete(null);
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					writer.TryComplete(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { writer.TryComplete(ex); }
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
#if NET5_0_OR_GREATER
					writer.TryComplete(ExceptionDispatchInfo.SetCurrentStackTrace(new OperationCanceledException()));
#else
					try { new OperationCanceledException(); }
					catch (Exception ex) { writer.TryComplete(ex); }
#endif
				}
				break;
			}
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
#endif
		private static unsafe void FindAllCallback(IntPtr handle, IntPtr context, NativeMethods.DeviceQueryResultActionData* action)
		{
#if NET5_0_OR_GREATER
			var (tcs, list) = Unsafe.As<Tuple<TaskCompletionSource, List<DeviceObjectInformation>>>(GCHandle.FromIntPtr(context).Target)!;
#else
			var (tcs, list) = Unsafe.As<Tuple<TaskCompletionSource<bool>, List<DeviceObjectInformation>>>(GCHandle.FromIntPtr(context).Target)!;
#endif
			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
#if NET6_0_OR_GREATER
				list.Add(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString()));
#else
				list.Add(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!));
#endif
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
#if NET5_0_OR_GREATER
					tcs.TrySetResult();
#else
					tcs.TrySetResult(true);
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					tcs.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { tcs.TrySetException(ex); }
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
					tcs.TrySetCanceled();
				}
				break;
			}
		}

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			EnumerateAllAsync(DeviceObjectKind.Unknown, filter, cancellationToken);

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceObjectKind objectKind, CancellationToken cancellationToken) =>
			EnumerateAllAsync(objectKind, null, cancellationToken);

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceObjectKind objectKind, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			GCHandle contextHandle;
			IntPtr helperContext;
			SafeDeviceQueryHandle query;
			ChannelReader<DeviceObjectInformation> reader;

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
				var channel = Channel.CreateUnbounded<DeviceObjectInformation>(EnumerateAllChannelOptions);
				reader = channel.Reader;

				contextHandle = GCHandle.Alloc(channel.Writer);
				try
				{
					// Wrap the context in a helper that *needs* to be freed.
					helperContext = CreateFindAllHelperContext(contextHandle);

					try
					{
						query = CreateObjectQuery(objectKind, filterExpressions, helperContext);
					}
					catch
					{
						Marshal.FreeHGlobal(helperContext);
						throw;
					}
				}
				catch
				{
					contextHandle.Free();
					throw;
				}
			}
			finally
			{
				filter?.ReleaseExpressionResources();
			}

			return EnumerateAllAsync(query, reader, helperContext, contextHandle, cancellationToken);
		}

		private static async IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync
		(
			SafeDeviceQueryHandle queryHandle,
			ChannelReader<DeviceObjectInformation> reader,
			IntPtr helperContext,
			GCHandle contextHandle,
			[EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			try
			{
				try
				{
					try
					{
						while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
						{
							while (reader.TryRead(out var info))
							{
								yield return info;
							}
						}
					}
					finally
					{
						queryHandle.Dispose();
					}
				}
				finally
				{
					Marshal.FreeHGlobal(helperContext);
				}
			}
			finally
			{
				contextHandle.Free();
			}
		}

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			FindAllAsync(DeviceObjectKind.Unknown, filter, cancellationToken);

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, CancellationToken cancellationToken) =>
			FindAllAsync(objectKind, null, cancellationToken);

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			GCHandle contextHandle;
			IntPtr helperContext;
			SafeDeviceQueryHandle query;
#if NET5_0_OR_GREATER
			TaskCompletionSource tcs;
#else
			TaskCompletionSource<bool> tcs;
#endif
			List<DeviceObjectInformation> list;

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
#if NET5_0_OR_GREATER
				tcs = new TaskCompletionSource();
#else
				tcs = new TaskCompletionSource<bool>();
#endif
				list = new List<DeviceObjectInformation>();

				contextHandle = GCHandle.Alloc(Tuple.Create(tcs, list));
				try
				{
					// Wrap the context in a helper that *needs* to be freed.
					helperContext = CreateFindAllHelperContext(contextHandle);

					try
					{
						query = CreateObjectQuery(objectKind, filterExpressions, helperContext);
					}
					catch
					{
						Marshal.FreeHGlobal(helperContext);
						throw;
					}
				}
				catch
				{
					contextHandle.Free();
					throw;
				}
			}
			finally
			{
				filter?.ReleaseExpressionResources();
			}

			return FindAllAsync(query, tcs, list, helperContext, contextHandle, cancellationToken);
		}

		private static async Task<DeviceObjectInformation[]> FindAllAsync
		(
			SafeDeviceQueryHandle queryHandle,
#if NET5_0_OR_GREATER
			TaskCompletionSource tcs,
#else
			TaskCompletionSource<bool> tcs,
#endif
			List<DeviceObjectInformation> list,
			IntPtr helperContext,
			GCHandle contextHandle,
			CancellationToken cancellationToken
		)
		{
			try
			{
				try
				{
					try
					{
						using var registration = cancellationToken.Register(state => ((SafeDeviceQueryHandle)state).Dispose(), queryHandle, false);

						await tcs.Task.ConfigureAwait(false);
					}
					finally
					{
						// Could lead to a double dispose… is that a problem ?
						queryHandle.Dispose();
					}
				}
				finally
				{
					Marshal.FreeHGlobal(helperContext);
				}
			}
			finally
			{
				contextHandle.Free();
			}

			return list.ToArray();
		}

		private static unsafe IntPtr CreateEnumerateAllHelperContext(GCHandle contextHandle)
		{
#if NET5_0_OR_GREATER
			var storage = Marshal.AllocHGlobal(sizeof(NativeMethods.DevQueryHelperContext));

			*(NativeMethods.DevQueryHelperContext*)storage = new NativeMethods.DevQueryHelperContext
			{
				Callback = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void>)&EnumerateAllCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			return storage;
#else
			var helperContext = new NativeMethods.DevQueryHelperContext()
			{
				Callback = EnumerateAllCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			var storage = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.DevQueryHelperContext>());
			Marshal.StructureToPtr(helperContext, storage, false);
			return storage;
#endif
		}

		private static unsafe IntPtr CreateFindAllHelperContext(GCHandle contextHandle)
		{
#if NET5_0_OR_GREATER
			var storage = Marshal.AllocHGlobal(sizeof(NativeMethods.DevQueryHelperContext));

			*(NativeMethods.DevQueryHelperContext*)storage = new NativeMethods.DevQueryHelperContext
			{
				Callback = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void>)&FindAllCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			return storage;
#else
			var helperContext = new NativeMethods.DevQueryHelperContext()
			{
				Callback = FindAllCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			var storage = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.DevQueryHelperContext>());
			Marshal.StructureToPtr(helperContext, storage, false);
			return storage;
#endif
		}

		private static unsafe SafeDeviceQueryHandle CreateObjectQuery(DeviceObjectKind kind, Span<NativeMethods.DevicePropertyFilterExpression> filters, IntPtr context)
		{
			return NativeMethods.DeviceCreateObjectQuery
			(
				kind,
				NativeMethods.DeviceQueryFlags.UpdateResults,
				0,
				ref Unsafe.NullRef<NativeMethods.DevicePropertyCompoundKey>(),
				filters.Length,
				ref MemoryMarshal.GetReference(filters),
#if NET5_0_OR_GREATER
				(delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void>)NativeMethods.NativeDevQueryCallback,
#else
				NativeMethods.NativeDevQueryCallback,
#endif
				context
			);
		}
	}
}
