using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
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
				writer.TryWrite(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString(), ParseProperties(ref @object)));
#else
				writer.TryWrite(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!, ParseProperties(ref @object)));
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
				list.Add(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString(), ParseProperties(ref @object)));
#else
				list.Add(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!, ParseProperties(ref @object)));
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

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
#endif
		private static unsafe void GetObjectPropertiesCallback(IntPtr handle, IntPtr context, NativeMethods.DeviceQueryResultActionData* action)
		{
			var ctx = Unsafe.As<GetPropertiesContext>(GCHandle.FromIntPtr(context).Target)!;

			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
				ctx.Properties = ParseProperties(ref @object);
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
#if NET5_0_OR_GREATER
					ctx.TaskCompletionSource.TrySetResult();
#else
					ctx.TaskCompletionSource.TrySetResult(true);
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					ctx.TaskCompletionSource.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { ctx.TaskCompletionSource.TrySetException(ex); }
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
					ctx.TaskCompletionSource.TrySetCanceled();
				}
				break;
			}
		}

		private sealed class GetPropertiesContext
		{
#if NET5_0_OR_GREATER
			public TaskCompletionSource TaskCompletionSource { get; }
#else
			public TaskCompletionSource<bool> TaskCompletionSource { get; }
#endif
			public Dictionary<PropertyKey, object?>? Properties { get; set; }

			public GetPropertiesContext() => TaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		private static readonly object False = false;
		private static readonly object True = true;

		private static unsafe Dictionary<PropertyKey, object?>? ParseProperties(ref NativeMethods.DeviceObject device)
		{
			int count = (int)device.PropertyCount;

			if (count < 0) return null;

			var dictionary = new Dictionary<PropertyKey, object?>(count);
			List<string>? stringList = null;

			for (int i = 0; i < count; i++)
			{
				ref var property = ref device.Properties[i];
				object? value;
				switch (property.Type)
				{
				case NativeMethods.DevicePropertyType.Null:
					value = null;
					break;
				case NativeMethods.DevicePropertyType.Boolean:
					value = *(sbyte*)property.Buffer != 0 ? True : False;
					break;
				case NativeMethods.DevicePropertyType.SByte:
					value = *(sbyte*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Byte:
					value = *(byte*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Int16:
					value = *(short*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.UInt16:
					value = *(ushort*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Int32:
					value = *(int*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.UInt32:
					value = *(uint*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Int64:
					value = *(long*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.UInt64:
					value = *(ulong*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Float:
					value = *(float*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Double:
					value = *(double*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.Guid:
					value = *(Guid*)property.Buffer;
					break;
				case NativeMethods.DevicePropertyType.FileTime:
					value = DateTime.FromFileTimeUtc(*(long*)property.Buffer);
					break;
				case NativeMethods.DevicePropertyType.String:
					value = property.BufferLength == 0 ? string.Empty : Marshal.PtrToStringUni(property.Buffer, (int)(property.BufferLength / 2) - 1)!;
					break;
				case NativeMethods.DevicePropertyType.StringList:
					stringList ??= new();

					var remaining = new ReadOnlySpan<char>((void*)property.Buffer, (int)property.BufferLength);

					while (remaining.Length > 0)
					{
						int end = remaining.IndexOf('\0');
						if (end < 1) break;

						stringList.Add(remaining.Slice(0, end).ToString());
						remaining = remaining.Slice(end + 1);
					}

					value = stringList.ToArray();
					stringList.Clear();
					break;
				case NativeMethods.DevicePropertyType.Binary:
					value = property.BufferLength == 0 ? Array.Empty<byte>() : new ReadOnlySpan<byte>((void*)property.Buffer, (int)property.BufferLength).ToArray();
					break;
				default:
					// Skip properties with unknown data types.
					continue;
				}

				dictionary.Add(property.CompoundKey.Key, value);
			}

			return dictionary;
		}

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceObjectKind objectKind, CancellationToken cancellationToken) =>
			EnumerateAllAsync(objectKind, null, null, cancellationToken);

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceObjectKind objectKind, DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			EnumerateAllAsync(objectKind, null, filter, cancellationToken);

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync(DeviceObjectKind objectKind, IEnumerable<Property>? properties, CancellationToken cancellationToken) =>
			EnumerateAllAsync(objectKind, properties, null, cancellationToken);

		public static IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync
		(
			DeviceObjectKind objectKind,
			IEnumerable<Property>? properties,
			DeviceFilterExpression? filter,
			CancellationToken cancellationToken
		)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = properties is null ?
					new Span<NativeMethods.DevicePropertyCompoundKey>() :
					properties.Select(p => new NativeMethods.DevicePropertyCompoundKey { Key = p.Key }).ToArray().AsSpan();

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
						query = CreateObjectQuery
						(
							objectKind,
							properties is null ? NativeMethods.DeviceQueryFlags.AllProperties : NativeMethods.DeviceQueryFlags.None,
							propertyKeys,
							filterExpressions,
							helperContext
						);
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

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, CancellationToken cancellationToken) =>
			FindAllAsync(objectKind, null, null, cancellationToken);

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			FindAllAsync(objectKind, null, filter, cancellationToken);

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, IEnumerable<Property>? properties, CancellationToken cancellationToken) =>
			FindAllAsync(objectKind, properties, null, cancellationToken);

		public static Task<DeviceObjectInformation[]> FindAllAsync(DeviceObjectKind objectKind, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = properties is null ?
				new Span<NativeMethods.DevicePropertyCompoundKey>() :
				properties.Select(p => new NativeMethods.DevicePropertyCompoundKey { Key = p.Key }).ToArray().AsSpan();

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
				tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#else
				tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
#endif
				list = new List<DeviceObjectInformation>();

				contextHandle = GCHandle.Alloc(Tuple.Create(tcs, list));
				try
				{
					// Wrap the context in a helper that *needs* to be freed.
					helperContext = CreateFindAllHelperContext(contextHandle);

					try
					{
						query = CreateObjectQuery
						(
							objectKind,
							properties is null ? NativeMethods.DeviceQueryFlags.AllProperties : NativeMethods.DeviceQueryFlags.None,
							propertyKeys,
							filterExpressions,
							helperContext
						);
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

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId, null, null, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId, null, filter, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, IEnumerable<Property>? properties, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId, properties, null, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			// We do not stricly need to do this check, but it will be more helpful than just returning nothing.
			// Anyway, if there is an error in this code, it will do no harm, as the string version will still work, maybe just less efficiently.
			if (objectKind is not (DeviceObjectKind.DeviceContainer or DeviceObjectKind.DeviceInterfaceClass or DeviceObjectKind.AssociationEndpointContainer or DeviceObjectKind.DeviceInterfaceClass or DeviceObjectKind.DeviceContainerDisplay))
			{
				throw new ArgumentException($"GUID object IDs are not valid for objects of type {objectKind}.");
			}

#if !NETSTANDARD2_0
			Span<char> guidString = stackalloc char[39];

			objectId.TryFormat(guidString, out _, "B");
			guidString[38] = '\0';
#else
			var guidString = objectId.ToString("B", CultureInfo.InvariantCulture).AsSpan();
#endif

			return GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(guidString), properties, filter, cancellationToken);
		}

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId[0], null, null, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, DeviceFilterExpression filter, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId[0], null, filter, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, IEnumerable<Property>? properties, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId[0], properties, null, cancellationToken);

		public static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId[0], properties, filter, cancellationToken);

		// NB: objectId must be null-terminated, which is the case for .NET strings.
		private static Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync(DeviceObjectKind objectKind, in char objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = properties is null ?
				new Span<NativeMethods.DevicePropertyCompoundKey>() :
				properties.Select(p => new NativeMethods.DevicePropertyCompoundKey { Key = p.Key }).ToArray().AsSpan();

			if (properties is not null && propertyKeys.Length == 0)
			{
				throw new ArgumentException("At least one property should be specified.");
			}

			GCHandle contextHandle;
			IntPtr helperContext;
			SafeDeviceQueryHandle query;
			GetPropertiesContext context;

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
				context = new();
				contextHandle = GCHandle.Alloc(context);
				try
				{
					// Wrap the context in a helper that *needs* to be freed.
					helperContext = CreateGetObjectPropertiesHelperContext(contextHandle);

					try
					{
						query = CreateObjectIdQuery
						(
							objectKind,
							objectId,
							properties is null ? NativeMethods.DeviceQueryFlags.AllProperties : NativeMethods.DeviceQueryFlags.None,
							propertyKeys,
							filterExpressions,
							helperContext
						);
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

			return GetObjectPropertiesAsync(query, context, helperContext, contextHandle, cancellationToken);
		}

		private static async Task<ReadOnlyDictionary<PropertyKey, object?>> GetObjectPropertiesAsync
		(
			SafeDeviceQueryHandle queryHandle,
			GetPropertiesContext context,
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

						await context.TaskCompletionSource.Task.ConfigureAwait(false);
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

			return context.Properties is not null ?
				new ReadOnlyDictionary<PropertyKey, object?>(context.Properties) :
				DeviceObjectInformation.EmptyProperties;
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

		private static unsafe IntPtr CreateGetObjectPropertiesHelperContext(GCHandle contextHandle)
		{
#if NET5_0_OR_GREATER
			var storage = Marshal.AllocHGlobal(sizeof(NativeMethods.DevQueryHelperContext));

			*(NativeMethods.DevQueryHelperContext*)storage = new NativeMethods.DevQueryHelperContext
			{
				Callback = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void>)&GetObjectPropertiesCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			return storage;
#else
			var helperContext = new NativeMethods.DevQueryHelperContext()
			{
				Callback = GetObjectPropertiesCallback,
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			var storage = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.DevQueryHelperContext>());
			Marshal.StructureToPtr(helperContext, storage, false);
			return storage;
#endif
		}

		private static unsafe SafeDeviceQueryHandle CreateObjectQuery(DeviceObjectKind kind, NativeMethods.DeviceQueryFlags flags, Span<NativeMethods.DevicePropertyCompoundKey> properties, Span<NativeMethods.DevicePropertyFilterExpression> filters, IntPtr context)
		{
			return NativeMethods.DeviceCreateObjectQuery
			(
				kind,
				flags,
				properties.Length,
				ref MemoryMarshal.GetReference(properties),
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

		private static unsafe SafeDeviceQueryHandle CreateObjectIdQuery
		(
			DeviceObjectKind kind,
			in char objectId,
			NativeMethods.DeviceQueryFlags flags,
			Span<NativeMethods.DevicePropertyCompoundKey> properties,
			Span<NativeMethods.DevicePropertyFilterExpression> filters,
			IntPtr context
		)
		{
			return NativeMethods.DeviceCreateObjectQueryFromId
			(
				kind,
				ref Unsafe.AsRef(objectId),
				flags,
				properties.Length,
				ref MemoryMarshal.GetReference(properties),
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
