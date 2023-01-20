using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using DeviceTools;
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
			var ctx = Unsafe.As<DevQueryCallbackContext<ChannelWriter<DeviceObjectInformation>>>(GCHandle.FromIntPtr(context).Target)!;
			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
#if NET6_0_OR_GREATER
				ctx.State.TryWrite(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString(), ParseProperties(ref @object)));
#else
				ctx.State.TryWrite(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!, ParseProperties(ref @object)));
#endif
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
					ctx.State.TryComplete(null);
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					ctx.State.TryComplete(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { ctx.State.TryComplete(ex); }
#endif
					ctx.Dispose();
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
#if NET5_0_OR_GREATER
					ctx.State.TryComplete(ExceptionDispatchInfo.SetCurrentStackTrace(new OperationCanceledException()));
#else
					try { new OperationCanceledException(); }
					catch (Exception ex) { ctx.State.TryComplete(ex); }
#endif
					ctx.Dispose();
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
			var ctx = Unsafe.As<DevQueryCallbackContext<TaskCompletionSource, List<DeviceObjectInformation>>>(GCHandle.FromIntPtr(context).Target)!;
#else
			var ctx = Unsafe.As<DevQueryCallbackContext<TaskCompletionSource<bool>, List<DeviceObjectInformation>>>(GCHandle.FromIntPtr(context).Target)!;
#endif
			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
				ctx.Value ??= new();
#if NET6_0_OR_GREATER
				ctx.Value!.Add(new(@object.ObjectType, MemoryMarshal.CreateReadOnlySpanFromNullTerminated(@object.ObjectId).ToString(), ParseProperties(ref @object)));
#else
				ctx.Value!.Add(new(@object.ObjectType, Marshal.PtrToStringUni((IntPtr)@object.ObjectId)!, ParseProperties(ref @object)));
#endif
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
#if NET5_0_OR_GREATER
					ctx.State.TrySetResult();
#else
					ctx.State.TrySetResult(true);
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					ctx.State.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { ctx.State.TrySetException(ex); }
#endif
					ctx.Dispose();
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
					ctx.State.TrySetCanceled();
					ctx.Dispose();
				}
				break;
			}
		}

#if NET5_0_OR_GREATER
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
#endif
		private static unsafe void GetObjectPropertiesCallback(IntPtr handle, IntPtr context, NativeMethods.DeviceQueryResultActionData* action)
		{
#if NET5_0_OR_GREATER
			var ctx = Unsafe.As<DevQueryCallbackContext<TaskCompletionSource, Dictionary<PropertyKey, object?>?>>(GCHandle.FromIntPtr(context).Target)!;
#else
			var ctx = Unsafe.As<DevQueryCallbackContext<TaskCompletionSource<bool>, Dictionary<PropertyKey, object?>?>>(GCHandle.FromIntPtr(context).Target)!;
#endif

			switch (action->Action)
			{
			case NativeMethods.DeviceQueryResultAction.DevQueryResultAdd:
				ref var @object = ref action->StateOrObject.DeviceObject;
				ctx.Value = ParseProperties(ref @object);
				break;
			case NativeMethods.DeviceQueryResultAction.DevQueryResultStateChange:
				var state = action->StateOrObject.State;
				if (state is NativeMethods.DeviceQueryState.DevQueryStateEnumCompleted)
				{
#if NET5_0_OR_GREATER
					ctx.State.TrySetResult();
#else
					ctx.State.TrySetResult(true);
#endif
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateAborted)
				{
#if NET5_0_OR_GREATER
					ctx.State.TrySetException(ExceptionDispatchInfo.SetCurrentStackTrace(new Exception("The query was aborted.")));
#else
					try { new Exception("The query was aborted."); }
					catch (Exception ex) { ctx.State.TrySetException(ex); }
#endif
					ctx.Dispose();
				}
				else if (state is NativeMethods.DeviceQueryState.DevQueryStateClosed)
				{
					ctx.State.TrySetCanceled();
					ctx.Dispose();
				}
				break;
			}
		}

		private enum Method
		{
			EnumerateAll = 1,
			FindAll,
			GetObjectProperties
		}

		// We only need to have this class to keep track of the data for the native helper and be able to release it… 
		private class DevQueryCallbackContext : IDisposable
		{
			private readonly IntPtr _helperContext;

			public DevQueryCallbackContext(Method method)
			{
				var gcHandle = GCHandle.Alloc(this);
				try
				{
					_helperContext = CreateHelperContext(gcHandle, method);
				}
				catch
				{
					gcHandle.Free();
					throw;
				}
			}

			public unsafe void Dispose()
			{
				GCHandle.FromIntPtr(((NativeMethods.DevQueryHelperContext*)_helperContext)->Context).Free();
				Marshal.FreeHGlobal(_helperContext);
			}

			internal IntPtr GetHandle() => _helperContext;
		}

		private class DevQueryCallbackContext<TState> : DevQueryCallbackContext
			where TState : class
		{
			public TState State { get; }

			public DevQueryCallbackContext(Method method, TState state) : base(method) => State = state;
		}

		private class DevQueryCallbackContext<TState, TValue> : DevQueryCallbackContext<TState>
			where TState : class
		{
			public TValue? Value { get; set; }

			public DevQueryCallbackContext(Method method, TState state) : base(method, state) { }
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
				case NativeMethods.DevicePropertyType.StringResource:
				case NativeMethods.DevicePropertyType.SecurityDescriptorString:
					string text = property.BufferLength == 0 ? string.Empty : Marshal.PtrToStringUni(property.Buffer, (int)(property.BufferLength / 2) - 1)!;
					if (property.Type == NativeMethods.DevicePropertyType.SecurityDescriptorString)
					{
						value = new RawSecurityDescriptor(text);
					}
					if (property.Type == NativeMethods.DevicePropertyType.StringResource)
					{
						value = new StringResource(text);
					}
					else
					{
						value = text;
					}
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
				case NativeMethods.DevicePropertyType.SecurityDescriptor:
				case NativeMethods.DevicePropertyType.Binary:
					var bytes = property.BufferLength == 0 ? Array.Empty<byte>() : new ReadOnlySpan<byte>((void*)property.Buffer, (int)property.BufferLength).ToArray();

					if (property.Type == NativeMethods.DevicePropertyType.SecurityDescriptor)
					{
						value = new RawSecurityDescriptor(bytes, 0);
					}
					else
					{
						value = bytes;
					}
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

			SafeDeviceQueryHandle query;
			DevQueryCallbackContext<ChannelReader<DeviceObjectInformation>> context;

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
				var channel = Channel.CreateUnbounded<DeviceObjectInformation>(EnumerateAllChannelOptions);
				context = new(Method.EnumerateAll, channel.Reader);

				try
				{
					query = CreateObjectQuery
					(
						objectKind,
						properties is null ? NativeMethods.DeviceQueryFlags.AllProperties | NativeMethods.DeviceQueryFlags.AsyncClose : NativeMethods.DeviceQueryFlags.AsyncClose,
						propertyKeys,
						filterExpressions,
						context.GetHandle()
					);
				}
				catch
				{
					context.Dispose();
					throw;
				}
			}
			finally
			{
				filter?.ReleaseExpressionResources();
			}

			return EnumerateAllAsync(query, context, cancellationToken);
		}

		private static async IAsyncEnumerable<DeviceObjectInformation> EnumerateAllAsync
		(
			SafeDeviceQueryHandle queryHandle,
			DevQueryCallbackContext<ChannelReader<DeviceObjectInformation>> context,
			[EnumeratorCancellation] CancellationToken cancellationToken
		)
		{
			try
			{
				while (await context.State.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
				{
					while (context.State.TryRead(out var info))
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

			SafeDeviceQueryHandle query;
#if NET5_0_OR_GREATER
			DevQueryCallbackContext<TaskCompletionSource, List<DeviceObjectInformation>> context;
#else
			DevQueryCallbackContext<TaskCompletionSource<bool>, List<DeviceObjectInformation>> context;
#endif

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
				context = new(Method.FindAll, new(TaskCreationOptions.RunContinuationsAsynchronously));

				query = CreateObjectQuery
				(
					objectKind,
					properties is null ? NativeMethods.DeviceQueryFlags.AllProperties | NativeMethods.DeviceQueryFlags.AsyncClose : NativeMethods.DeviceQueryFlags.AsyncClose,
					propertyKeys,
					filterExpressions,
					context.GetHandle()
				);
			}
			finally
			{
				filter?.ReleaseExpressionResources();
			}

			return FindAllAsync(query, context, cancellationToken);
		}

		private static async Task<DeviceObjectInformation[]> FindAllAsync
		(
			SafeDeviceQueryHandle queryHandle,
#if NET5_0_OR_GREATER
			DevQueryCallbackContext<TaskCompletionSource, List<DeviceObjectInformation>> context,
#else
			DevQueryCallbackContext<TaskCompletionSource<bool>, List<DeviceObjectInformation>> context,
#endif
			CancellationToken cancellationToken
		)
		{
			try
			{
				using var registration = cancellationToken.Register(state => ((SafeDeviceQueryHandle)state).Dispose(), queryHandle, false);

				await context.State.Task.ConfigureAwait(false);
			}
			finally
			{
				// Could lead to a double dispose… is that a problem ?
				queryHandle.Dispose();
			}

			return context.Value.ToArray();
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, objectId, null as IEnumerable<Property>, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, DeviceFilterExpression filter, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, objectId, null as IEnumerable<Property>, filter, cancellationToken);

		public static Task<object?> GetObjectPropertyAsync(DeviceObjectKind objectKind, Guid objectId, Property property, CancellationToken cancellationToken)
			=> GetObjectPropertyAsync(objectKind, objectId, property, null, cancellationToken);

		public static Task<object?> GetObjectPropertyAsync(DeviceObjectKind objectKind, Guid objectId, Property property, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[1];

			propertyKeys[0].Key = property.Key;

			return GetObjectPropertyAsync(GetObjectPropertiesAsync(objectKind, objectId, propertyKeys, filter, cancellationToken), property);
		}

		// This helper method avoids boilerplate stuff when you need to query a single property. It will not avoid allocations but it is way more convenient to use.
		public static Task<TValue?> GetObjectPropertyAsync<TValue>(DeviceObjectKind objectKind, Guid objectId, Property<TValue?> property, CancellationToken cancellationToken)
			=> GetObjectPropertyAsync(objectKind, objectId, property, null, cancellationToken);

		public static Task<TValue?> GetObjectPropertyAsync<TValue>(DeviceObjectKind objectKind, Guid objectId, Property<TValue?> property, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[1];

			propertyKeys[0].Key = property.Key;

			return GetObjectPropertyAsync<TValue>(GetObjectPropertiesAsync(objectKind, objectId, propertyKeys, filter, cancellationToken), property);
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, Property property1, Property property2, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, objectId, property1, property2, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, Property property1, Property property2, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[2];

			propertyKeys[0].Key = property1.Key;
			propertyKeys[1].Key = property2.Key;

			return GetObjectPropertiesAsync(objectKind, objectId, propertyKeys, filter, cancellationToken);
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, IEnumerable<Property>? properties, CancellationToken cancellationToken) =>
			GetObjectPropertiesAsync(objectKind, objectId, properties, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
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

		private static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, Guid objectId, Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys, DeviceFilterExpression? filter, CancellationToken cancellationToken)
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

			return GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(guidString), propertyKeys, filter, cancellationToken);
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), null as IEnumerable<Property>, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, DeviceFilterExpression filter, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), null as IEnumerable<Property>, filter, cancellationToken);

		public static Task<object?> GetObjectPropertyAsync(DeviceObjectKind objectKind, string objectId, Property property, CancellationToken cancellationToken)
			=> GetObjectPropertyAsync(objectKind, objectId, property, null, cancellationToken);

		public static Task<object?> GetObjectPropertyAsync(DeviceObjectKind objectKind, string objectId, Property property, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[1];

			propertyKeys[0].Key = property.Key;

			return GetObjectPropertyAsync(GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), propertyKeys, filter, cancellationToken), property);
		}

		private static async Task<object?> GetObjectPropertyAsync(Task<DevicePropertyDictionary> task, Property property)
		{
			var properties = await task.ConfigureAwait(false);

			properties.TryGetValue(property.Key, out var value);

			return value;
		}

		// This helper method avoids boilerplate stuff when you need to query a single property. It will not avoid allocations but it is way more convenient to use.
		public static Task<TValue?> GetObjectPropertyAsync<TValue>(DeviceObjectKind objectKind, string objectId, Property<TValue?> property, CancellationToken cancellationToken)
			=> GetObjectPropertyAsync(objectKind, objectId, property, null, cancellationToken);

		public static Task<TValue?> GetObjectPropertyAsync<TValue>(DeviceObjectKind objectKind, string objectId, Property<TValue?> property, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[1];

			propertyKeys[0].Key = property.Key;

			return GetObjectPropertyAsync<TValue>(GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), propertyKeys, filter, cancellationToken), property);
		}

		private static async Task<TValue?> GetObjectPropertyAsync<TValue>(Task<DevicePropertyDictionary> task, Property property)
		{
			var properties = await task.ConfigureAwait(false);

			properties.TryGetValue<TValue>(property.Key, out var value);

			return value;
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, Property property1, Property property2, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, objectId, property1, property2, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, Property property1, Property property2, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = stackalloc NativeMethods.DevicePropertyCompoundKey[2];

			propertyKeys[0].Key = property1.Key;
			propertyKeys[1].Key = property2.Key;

			return GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), propertyKeys, filter, cancellationToken);
		}

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, IEnumerable<Property>? properties, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), properties, null, cancellationToken);

		public static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, string objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
			=> GetObjectPropertiesAsync(objectKind, MemoryMarshal.GetReference(objectId.AsSpan()), properties, filter, cancellationToken);

		private static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, in char objectId, IEnumerable<Property>? properties, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys = properties is null ?
				new Span<NativeMethods.DevicePropertyCompoundKey>() :
				properties
					.Select(p => new NativeMethods.DevicePropertyCompoundKey { Key = p.Key })
					.ToArray()
					.AsSpan();

			if (properties is not null && propertyKeys.IsEmpty)
			{
				throw new ArgumentException("At least one property should be specified.");
			}

			return GetObjectPropertiesAsync(objectKind, objectId, propertyKeys, filter, cancellationToken);
		}

		// NB: objectId must be null-terminated, which is the case for .NET strings.
		private static Task<DevicePropertyDictionary> GetObjectPropertiesAsync(DeviceObjectKind objectKind, in char objectId, Span<NativeMethods.DevicePropertyCompoundKey> propertyKeys, DeviceFilterExpression? filter, CancellationToken cancellationToken)
		{
			int count = filter?.GetFilterElementCount(true) ?? 0;
			Span<NativeMethods.DevicePropertyFilterExpression> filterExpressions = count <= 4 ?
				count == 0 ?
					new Span<NativeMethods.DevicePropertyFilterExpression>() :
					stackalloc NativeMethods.DevicePropertyFilterExpression[count] :
				new NativeMethods.DevicePropertyFilterExpression[count];

			SafeDeviceQueryHandle query;
#if NET5_0_OR_GREATER
			DevQueryCallbackContext<TaskCompletionSource, Dictionary<PropertyKey, object?>?> context;
#else
			DevQueryCallbackContext<TaskCompletionSource<bool>, Dictionary<PropertyKey, object?>?> context;
#endif

			filter?.FillExpressions(filterExpressions, true, out count);

			try
			{
				context = new(Method.GetObjectProperties, new(TaskCreationOptions.RunContinuationsAsynchronously));
				query = CreateObjectIdQuery
				(
					objectKind,
					objectId,
					propertyKeys.IsEmpty ? NativeMethods.DeviceQueryFlags.AllProperties | NativeMethods.DeviceQueryFlags.AsyncClose : NativeMethods.DeviceQueryFlags.AsyncClose,
					propertyKeys,
					filterExpressions,
					context.GetHandle()
				);
			}
			finally
			{
				filter?.ReleaseExpressionResources();
			}

			return GetObjectPropertiesAsync(query, context, cancellationToken);
		}

		private static async Task<DevicePropertyDictionary> GetObjectPropertiesAsync
		(
			SafeDeviceQueryHandle queryHandle,
#if NET5_0_OR_GREATER
			DevQueryCallbackContext<TaskCompletionSource, Dictionary<PropertyKey, object?>?> context,
#else
			DevQueryCallbackContext<TaskCompletionSource<bool>, Dictionary<PropertyKey, object?>?> context,
#endif
			CancellationToken cancellationToken
		)
		{
			try
			{
				using var registration = cancellationToken.Register(state => ((SafeDeviceQueryHandle)state).Dispose(), queryHandle, false);

				await context.State.Task.ConfigureAwait(false);
			}
			finally
			{
				// Could lead to a double dispose… is that a problem ?
				queryHandle.Dispose();
			}

			return context.Value is not null ?
				new DevicePropertyDictionary(context.Value) :
				DeviceObjectInformation.EmptyProperties;
		}

		private static unsafe IntPtr CreateHelperContext(GCHandle contextHandle, Method method)
		{
#if NET5_0_OR_GREATER
			var storage = Marshal.AllocHGlobal(sizeof(NativeMethods.DevQueryHelperContext));

			*(NativeMethods.DevQueryHelperContext*)storage = new NativeMethods.DevQueryHelperContext
			{
				Callback = method switch
				{
					Method.EnumerateAll => &EnumerateAllCallback,
					Method.FindAll => &FindAllCallback,
					Method.GetObjectProperties => &GetObjectPropertiesCallback,
					_ => throw new InvalidOperationException()
				},
				Context = GCHandle.ToIntPtr(contextHandle),
			};

			return storage;
#else
			var helperContext = new NativeMethods.DevQueryHelperContext()
			{
				Callback = method switch
				{
					Method.EnumerateAll => EnumerateAllCallback,
					Method.FindAll => FindAllCallback,
					Method.GetObjectProperties => GetObjectPropertiesCallback,
					_ => throw new InvalidOperationException()
				},
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
