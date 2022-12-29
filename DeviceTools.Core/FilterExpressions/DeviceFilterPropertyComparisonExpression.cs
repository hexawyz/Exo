using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DeviceTools.FilterExpressions
{
	public abstract class DeviceFilterPropertyComparisonExpression : DeviceFilterExpression
	{
		// Represents the a cache of various commonly used values.
		private readonly struct CachedValues
		{
			public readonly ulong ValueMinusOne;
			public readonly ulong Value0;
			public readonly ulong Value1;
			public readonly ulong Value2;
			public readonly ulong Value3;
			public readonly ulong Value4;
			public readonly ulong Value5;
			public readonly ulong Value6;
			public readonly ulong Value7;
			public readonly ulong Value8;
			public readonly ulong Value9;
			public readonly ulong Value10;
			public readonly ulong Value16;
			public readonly ulong Value24;
			public readonly ulong Value32;
			public readonly ulong Value64;
			public readonly ulong Value128;
			public readonly ulong Value256;

			public CachedValues()
			{
				// Also serves as True
				ValueMinusOne = ulong.MaxValue;
				// Also serves as False
				Value0 = 0;
				Value1 = 1;
				Value2 = 2;
				Value3 = 3;
				Value4 = 4;
				Value5 = 5;
				Value6 = 6;
				Value7 = 7;
				Value8 = 8;
				Value9 = 9;
				Value10 = 10;
				Value16 = 16;
				Value24 = 24;
				Value32 = 32;
				Value64 = 64;
				Value128 = 128;
				Value256 = 256;
			}
		}

		private static readonly CachedValues[] ValueCache = AllocateValueCache();
#if !NET5_0_OR_GREATER
		// This will pin the values in memory once and for all, but also prevent the whole library from being collected.
		private static readonly GCHandle ValueCacheHandle = GCHandle.Alloc(ValueCache, GCHandleType.Pinned);
#endif

		private static CachedValues[] AllocateValueCache()
		{
#if NET5_0_OR_GREATER
			var cache = GC.AllocateArray<CachedValues>(1, pinned: true);
#else
			var cache = new CachedValues[1];
#endif
			cache[0] = new();
			return cache;
		}

		private static unsafe CachedValues* GetValueCachePointer() => (CachedValues*)Unsafe.AsPointer(ref ValueCache[0]);

		private protected static unsafe bool TryGetCachedValueAddress(ulong value, int offset, out IntPtr address)
		{
			var cache = GetValueCachePointer();
			var rawAddress = (IntPtr)
			(
				value switch
				{
					0 => &cache->Value0,
					1 => &cache->Value1,
					2 => &cache->Value2,
					3 => &cache->Value3,
					4 => &cache->Value4,
					5 => &cache->Value5,
					6 => &cache->Value6,
					7 => &cache->Value7,
					8 => &cache->Value8,
					9 => &cache->Value9,
					10 => &cache->Value10,
					16 => &cache->Value16,
					24 => &cache->Value24,
					32 => &cache->Value32,
					64 => &cache->Value64,
					128 => &cache->Value128,
					256 => &cache->Value256,
					ulong.MaxValue => &cache->ValueMinusOne,
					_ => null
				}
			);

			if (rawAddress != IntPtr.Zero)
			{
				address = rawAddress + offset;
				return true;
			}
			else
			{
				address = IntPtr.Zero;
				return false;
			}
		}

		// We use the same UInt64 storage for caches of Int8/Int16/Int32/Int64, but we need to be careful about endianness when manually casting.
		private protected static int Int64ToByteOffset() => BitConverter.IsLittleEndian ? 0 : 7;
		private protected static int Int64ToInt16Offset() => BitConverter.IsLittleEndian ? 0 : 6;
		private protected static int Int64ToInt32Offset() => BitConverter.IsLittleEndian ? 0 : 4;

		public Property Property { get; }
		internal readonly NativeMethods.DevPropertyOperator _operator;

		private protected DeviceFilterPropertyComparisonExpression(Property property, NativeMethods.DevPropertyOperator @operator)
		{
			Property = property;
			_operator = @operator;
		}

		internal static DeviceFilterPropertyComparisonExpression Create<TProperty, TValue, TOperator>(TProperty property, TValue value, TOperator @operator)
			where TProperty : Property, IProperty<TValue>
			where TOperator : struct, Enum
			=> new DeviceFilterPropertyComparisonExpression<TProperty, TValue, TOperator>(property, value, @operator);

		internal abstract DeviceFilterPropertyComparisonExpression Not();
	}

	public abstract class DeviceFilterPropertyComparisonExpression<TValue, TOperator> : DeviceFilterPropertyComparisonExpression
		where TOperator : struct, Enum
	{

		// Status of the instance value cache. (Used during interop with DevQuery, when the native filters expressions need to be built and passed to the native code)
		// Default state: needs to be initialized.
		private const int StateUninitialized = 0;
		// The state is a constant cached value that does not need to be freed. (Should be allocated on the POH for recent .NET versions)
		private const int StateCachedValue = 1;
		// The state is a GCHandle that is pinning the object in place. The value can be found in _value anyway.
		private const int StatePinnedObject = 2;

		// TODO: Maybe move these in a separate object ?
		// Numnber of external references to the value represented by this parameter. (Can be more than one if the expression is cached and used concurrently)
		private int _refCount;
		// The current state of the value.
		private int _state;
		// A GC Handle or a memory address depending on the state.
		private IntPtr _gcHandleOrMemoryAddress;

		// TValue or StrongBox<TValue>
		// We have to box value types in otder to pin the values.
		// Pinning this instance cannot be done because Property is a reference, and prevents GCHandleType.Pinned from working as we would like to.
		internal readonly object _value;
		public TValue Value => typeof(TValue).IsValueType ? Unsafe.As<StrongBox<TValue>>(_value).Value! : Unsafe.As<object, TValue>(ref Unsafe.AsRef(_value));

		internal static NativeMethods.DevPropertyOperator ConvertOperator(TOperator @operator) => Unsafe.As<TOperator, NativeMethods.DevPropertyOperator>(ref @operator);
		internal static TOperator ConvertOperator(NativeMethods.DevPropertyOperator @operator) => Unsafe.As<NativeMethods.DevPropertyOperator, TOperator>(ref @operator);

		private protected DeviceFilterPropertyComparisonExpression(Property property, TValue value, TOperator @operator)
			: base(property, ConvertOperator(@operator))
		{
			// Store the value in this instance.
			if (typeof(TValue).IsValueType)
			{
				_value = new StrongBox<TValue>(value);
			}
			else
			{
				_value = Unsafe.As<TValue, object>(ref value);
			}

			// Pre-initialize the state for cached values, as they are constant and do not need to be deallocated.
			// While we (sadly) don't avoid the value boxing here, this still prevents allocating a GCHandle, so it will have some benefits.
			// Also some types (bool) require special treatment anyway.
			if
			(
				typeof(TValue) == typeof(bool) && TryGetCachedValueAddress(Unsafe.As<TValue, bool>(ref value) ? ulong.MaxValue : ulong.MinValue, Int64ToByteOffset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(byte) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, byte>(ref value), Int64ToByteOffset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(sbyte) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, sbyte>(ref value), Int64ToByteOffset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(ushort) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, ushort>(ref value), Int64ToInt16Offset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(short) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, short>(ref value), Int64ToInt16Offset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(uint) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, uint>(ref value), Int64ToInt32Offset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(int) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, int>(ref value), Int64ToInt32Offset(), out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(ulong) && TryGetCachedValueAddress((ulong)(long)Unsafe.As<TValue, ulong>(ref value), 0, out _gcHandleOrMemoryAddress) ||
				typeof(TValue) == typeof(long) && TryGetCachedValueAddress((ulong)Unsafe.As<TValue, long>(ref value), 0, out _gcHandleOrMemoryAddress)
			)
			{
				Volatile.Write(ref _state, StateCachedValue);
			}
			else if (typeof(TValue) == typeof(string))
			{
				// We can have a pointer to a cached value of '\0' for empty strings, thus also avoiding GCHandle in that case.
				var s = Unsafe.As<TValue, string>(ref value);
				if (s is { Length: 0 } && TryGetCachedValueAddress(0, Int64ToInt16Offset(), out _gcHandleOrMemoryAddress))
				{
					Volatile.Write(ref _state, StateCachedValue);
				}
			}
			GC.SuppressFinalize(this);
		}

		~DeviceFilterPropertyComparisonExpression()
		{
			if (_value is object @lock)
			{
				if (Volatile.Read(ref _state) == StateCachedValue) return;

				lock (@lock)
				{
					InternalFree();
					_refCount = 0;
				}
			}
		}

		private void InternalFree()
		{
			if (_state == StatePinnedObject)
			{
				GCHandle.FromIntPtr(_gcHandleOrMemoryAddress).Free();
			}
			_gcHandleOrMemoryAddress = IntPtr.Zero;
			_state = StateUninitialized;
		}

		internal sealed override int GetFilterElementCount(bool isRoot) => 1;

		private int GetBufferLength()
		{
			int dataLength;
			if (_value == null)
			{
				dataLength = 0;
			}
			else if (typeof(TValue) == typeof(string))
			{
				dataLength = (Unsafe.As<string>(_value).Length + 1) * 2;
			}
			else if (Property.Type.IsFixedLength())
			{
				dataLength = Property.Type.GetLength();
			}
			else
			{
				throw new NotSupportedException($"The support for data type {typeof(TValue)} is missing.");
			}

			return dataLength;
		}

		private unsafe IntPtr GetBuffer()
		{
			// State shouldn't change when GetBuffer is called, because it should be preceeded by a call to AddRef().
			int state = _state;

			if (typeof(TValue) == typeof(string))
			{
				if (_value == null)
				{
					return IntPtr.Zero;
				}

				return (IntPtr)Unsafe.AsPointer(ref Unsafe.AsRef(Unsafe.As<string>(_value)[0]));
			}
			else if (state == StateCachedValue)
			{
				return _gcHandleOrMemoryAddress;
			}
			else if (typeof(TValue).IsValueType)
			{
				return (IntPtr)Unsafe.AsPointer(ref Unsafe.As<StrongBox<TValue>>(_value).Value!);
			}

			throw new NotSupportedException($"The support for data type {typeof(TValue)} is missing.");
		}

		internal override void FillExpressions(Span<NativeMethods.DevicePropertyFilterExpression> expressions, bool isRoot, out int count)
		{
			AddRef();

			expressions[0] = new NativeMethods.DevicePropertyFilterExpression
			{
				Operator = _operator,
				Property =
				{
					CompoundKey =
					{
						Key = Property.Key,
						Store = NativeMethods.DevicePropertyStore.Sytem,
					},
					Type = _value is null ? NativeMethods.DevicePropertyType.Null : Property.Type.Value,
					BufferLength = (uint)GetBufferLength(),
					Buffer = GetBuffer(),
				}
			};

			count = 1;
		}

		private void AddRef()
		{
			int state = Volatile.Read(ref _state);

			if (state != StateCachedValue && _value is not null)
			{
				Interlocked.Increment(ref _refCount);

				lock (_value)
				{
					state = _state;
					if (state == StateUninitialized)
					{
						// We pin the value object no matter what its type is, as it is either a string or a StrongBox<TValue>.
						_gcHandleOrMemoryAddress = GCHandle.ToIntPtr(GCHandle.Alloc(Value, GCHandleType.Pinned));
						GC.ReRegisterForFinalize(this);
					}
				}
			}
		}

		internal override void ReleaseExpressionResources()
		{
			var state = Volatile.Read(ref _state);

			if (state == StateCachedValue || _value is null)
			{
				return;
			}

			if (Interlocked.Decrement(ref _refCount) == 0)
			{
				lock (_value)
				{
					if (_refCount != 0) return;

					InternalFree();

					GC.SuppressFinalize(this);
				}
			}
		}
	}

	internal class DeviceFilterPropertyComparisonExpression<TProperty, TValue, TOperator> : DeviceFilterPropertyComparisonExpression<TValue, TOperator>
		where TProperty : Property, IProperty<TValue>
		where TOperator : struct, Enum
	{
		public new TProperty Property => Unsafe.As<TProperty>(base.Property);

		internal DeviceFilterPropertyComparisonExpression(Property property, TValue value, TOperator @operator)
			: base(property, value, @operator)
		{
		}

		internal override DeviceFilterPropertyComparisonExpression Not() =>
			new DeviceFilterPropertyComparisonExpression<TProperty, TValue, TOperator>
			(
				Property,
				Value,
				ConvertOperator
				(
					_operator switch
					{
						NativeMethods.DevPropertyOperator.GreaterThan => NativeMethods.DevPropertyOperator.LessThanOrEquals,
						NativeMethods.DevPropertyOperator.LessThan => NativeMethods.DevPropertyOperator.GreaterThanOrEquals,
						NativeMethods.DevPropertyOperator.GreaterThanOrEquals => NativeMethods.DevPropertyOperator.LessThan,
						NativeMethods.DevPropertyOperator.LessThanOrEquals => NativeMethods.DevPropertyOperator.GreaterThan,
						var x => NativeMethods.DevPropertyOperator.ModifierNot ^ x,
					}
				)
			);
	}
}
