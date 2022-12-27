using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Channels;

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
		private static unsafe void FindAllCallback(IntPtr handle, IntPtr context, NativeMethods.DeviceQueryResultActionData* action)
		{
			var writer = Unsafe.As<ChannelWriter<DeviceObjectInformation>>(GCHandle.FromIntPtr(context).Target)!;
			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
#if NET5_0_OR_GREATER
				writer.TryWrite(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString()));
#else
				writer.TryWrite(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)));
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

		public static async IAsyncEnumerable<DeviceObjectInformation> FindAllAsync(Guid deviceInterfaceClassGuid, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var channel = Channel.CreateUnbounded<DeviceObjectInformation>(FindAllChannelOptions);
			var reader = channel.Reader;
			var contextHandle = GCHandle.Alloc(channel.Writer);

			try
			{
				// Wrap the context in a helper that *needs* to be freed.
				var helperContext = CreateHelperContext(contextHandle);

				try
				{
					using var query = CreateObjectQuery(DeviceObjectKind.DeviceInterface, deviceInterfaceClassGuid, helperContext);
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

		private static unsafe SafeDeviceQueryHandle CreateObjectQuery(DeviceObjectKind kind, Guid guid, IntPtr context)
		{
			//Span<NativeMethods.DevicePropertyFilterExpression> filters = stackalloc NativeMethods.DevicePropertyFilterExpression[1];
			var filters = new Span<NativeMethods.DevicePropertyFilterExpression>((void*)Marshal.AllocHGlobal(2 * sizeof(NativeMethods.DevicePropertyFilterExpression)), 2);
			var guidData = Marshal.AllocHGlobal(16);
			*(Guid*)guidData = guid;
			var trueData = Marshal.AllocHGlobal(1);
			*(sbyte*)trueData = -1;

			filters[0] = new NativeMethods.DevicePropertyFilterExpression
			{
				Operator = NativeMethods.DevPropertyOperator.Equals,
				Property =
				{
					CompoundKey =
					{
						Key = NativeMethods.DevicePropertyKeys.DeviceInterfaceClassGuid,
						Store = NativeMethods.DevicePropertyStore.Sytem
					},
					Type = NativeMethods.DevicePropertyType.Guid,
					Buffer = guidData,
					BufferLength = 16,
				}
			};
			filters[1] = new NativeMethods.DevicePropertyFilterExpression
			{
				Operator = NativeMethods.DevPropertyOperator.Equals,
				Property =
				{
					CompoundKey =
					{
						Key = NativeMethods.DevicePropertyKeys.DeviceInterfaceEnabled,
						Store = NativeMethods.DevicePropertyStore.Sytem
					},
					Type = NativeMethods.DevicePropertyType.Boolean,
					Buffer = trueData,
					BufferLength = 1,
				}
			};

			return NativeMethods.DeviceCreateObjectQuery
			(
				kind,
				NativeMethods.DeviceQueryFlags.UpdateResults,
				0,
				ref Unsafe.NullRef<NativeMethods.DevicePropertyCompoundKey>(),
				filters.Length,
				ref MemoryMarshal.GetReference(filters),
				//0,
				//ref Unsafe.NullRef<NativeMethods.DevicePropertyFilterExpression>(),
#if NET5_0_OR_GREATER
				(delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void>)NativeMethods.NativeDevQueryCallback,
#else
				NativeMethods.NativeDevQueryCallback,
#endif
				context
			);
		}
	}

	public enum DeviceObjectKind
	{
		Unknown = 0,
		DeviceInterface = 1,
		DeviceContainer = 2,
		Device = 3,
		DeviceInterfaceClass = 4,
		AssociationEndpoint = 5,
		AssociationEndpointContainer = 6,
		DeviceInstallerClass = 7,
		DeviceInterfaceDisplay = 8,
		DeviceContainerDisplay = 9,
		AssociationEndpointService = 10,
		DevicePanel = 11,
	}

	public sealed class DeviceObjectInformation
	{
		public DeviceObjectInformation(DeviceObjectKind kind, string id)
		{
			Kind = kind;
			Id = id;
		}

		public DeviceObjectKind Kind { get; }

		public string Id { get; }
	}
}
