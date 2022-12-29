using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;
using DeviceTools.FilterExpressions;

namespace DeviceTools
{
	public sealed class DeviceQuery
	{
		private static readonly UnboundedChannelOptions FindAllChannelOptions = new UnboundedChannelOptions()
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
				if (action->StateOrObject.State is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted or NativeMethods.DeviceQueryState.DevQueryStateAborted or NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
					writer.TryComplete(null);
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
				var channel = Channel.CreateUnbounded<DeviceObjectInformation>(FindAllChannelOptions);
				reader = channel.Reader;

				contextHandle = GCHandle.Alloc(channel.Writer);
				try
				{
					// Wrap the context in a helper that *needs* to be freed.
					helperContext = CreateHelperContext(contextHandle);

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

		private static unsafe IntPtr CreateHelperContext(GCHandle contextHandle)
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
