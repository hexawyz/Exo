using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.Bluetooth;

/// <summary>WIP API for Bluetooth LE. It is not ready for general consumption and will likely need refactoring.</summary>
public static class BluetoothLeDevice
{
	public static unsafe ImmutableArray<BluetoothLeServiceInformation> GetServices(SafeFileHandle deviceHandle)
	{
		ushort length = 0;
		uint result = NativeMethods.BluetoothGattGetServices(deviceHandle, 0, null, out length, NativeMethods.BluetoothGattFlags.None);
		if (result != NativeMethods.ErrorMoreData)
		{
			Marshal.ThrowExceptionForHR((int)result);
		}
		var services = new NativeMethods.BluetoothLeGattService[length];
		fixed (NativeMethods.BluetoothLeGattService* servicesPointer = services)
		{
			result = NativeMethods.BluetoothGattGetServices(deviceHandle, length, servicesPointer, out length, NativeMethods.BluetoothGattFlags.None);
		}
		if (result != 0)
		{
			Marshal.ThrowExceptionForHR((int)result);
		}
		return ConvertServices(services.AsSpan(length));
	}

	private static ImmutableArray<BluetoothLeServiceInformation> ConvertServices(ReadOnlySpan<NativeMethods.BluetoothLeGattService> services)
	{
		var convertedServices = new BluetoothLeServiceInformation[services.Length];
		for (int i = 0; i < services.Length; i++)
		{
			ref readonly var service = ref services[i];
			convertedServices[i] = new
			(
				service.ServiceUuid.IsShortUuid != 0 ?
					new BluetoothLeUuid(DeviceInterfaceClassGuids.GetBluetoothUuidGuid(service.ServiceUuid.Value.ShortUuid)) :
					new BluetoothLeUuid(service.ServiceUuid.Value.LongUuid),
				new(service.AttributeHandle)
			);
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(convertedServices);
	}

	public static unsafe ImmutableArray<BluetoothLeCharacteristicInformation> GetCharacteristics(SafeFileHandle serviceHandle)
	{
		ushort length = 0;
		uint result = NativeMethods.BluetoothGattGetCharacteristics(serviceHandle, null, 0, null, out length, NativeMethods.BluetoothGattFlags.None);
		if (result != NativeMethods.ErrorMoreData)
		{
			Marshal.ThrowExceptionForHR((int)result);
		}
		var characteristics = new NativeMethods.BluetoothLeGattCharacteristic[length];
		fixed (NativeMethods.BluetoothLeGattCharacteristic* characteristicsPointer = characteristics)
		{
			result = NativeMethods.BluetoothGattGetCharacteristics(serviceHandle, null, length, characteristicsPointer, out length, NativeMethods.BluetoothGattFlags.None);
		}
		if (result != 0)
		{
			Marshal.ThrowExceptionForHR((int)result);
		}
		return ConvertCharacteristics(characteristics.AsSpan(0, length));
	}

	private static ImmutableArray<BluetoothLeCharacteristicInformation> ConvertCharacteristics(ReadOnlySpan<NativeMethods.BluetoothLeGattCharacteristic> characteristics)
		=> MemoryMarshal.Cast<NativeMethods.BluetoothLeGattCharacteristic, BluetoothLeCharacteristicInformation>(characteristics).ToImmutableArray();

	public static unsafe void Write(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation characteristic, ReadOnlySpan<byte> data)
	{
		fixed (BluetoothLeCharacteristicInformation* characteristicPointer = &characteristic)
		{
			if (data.Length <= 28) WriteFromStack(serviceHandle, (NativeMethods.BluetoothLeGattCharacteristic*)characteristicPointer, data);
			else WriteFromArrayPool(serviceHandle, (NativeMethods.BluetoothLeGattCharacteristic*)characteristicPointer, data);
		}
	}

	public static unsafe void UnsafeWrite(SafeFileHandle serviceHandle, in BluetoothLeCharacteristicInformation characteristic, byte* rawDataWithLengthPrefix)
	{
		fixed (BluetoothLeCharacteristicInformation* characteristicPointer = &characteristic)
		{
			uint result = NativeMethods.BluetoothGattSetCharacteristicValue(serviceHandle, (NativeMethods.BluetoothLeGattCharacteristic*)characteristicPointer, rawDataWithLengthPrefix, 0, NativeMethods.BluetoothGattFlags.None);
			if (result != 0) Marshal.ThrowExceptionForHR((int)result);
		}
	}

	private static unsafe void WriteFromStack(SafeFileHandle serviceHandle, NativeMethods.BluetoothLeGattCharacteristic* characteristic, ReadOnlySpan<byte> data)
	{
		Span<byte> buffer = stackalloc byte[data.Length + 4];
		Write(serviceHandle, characteristic, data, buffer);
	}

	private static unsafe void WriteFromArrayPool(SafeFileHandle serviceHandle, NativeMethods.BluetoothLeGattCharacteristic* characteristic, ReadOnlySpan<byte> data)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(4 + data.Length, 48));
		try
		{
			Unsafe.As<byte, int>(ref buffer[0]) = data.Length;
			data.CopyTo(buffer.AsSpan(4));
			fixed (byte* dataPointer = data)
			{
				NativeMethods.BluetoothGattSetCharacteristicValue(serviceHandle, characteristic, dataPointer, 0, NativeMethods.BluetoothGattFlags.None);
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	private static unsafe void Write(SafeFileHandle serviceHandle, NativeMethods.BluetoothLeGattCharacteristic* characteristic, ReadOnlySpan<byte> data, Span<byte> buffer)
	{
		Unsafe.As<byte, int>(ref buffer[0]) = data.Length;
		data.CopyTo(buffer.Slice(4));
		uint result = NativeMethods.BluetoothGattSetCharacteristicValue(serviceHandle, characteristic, (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer)), 0, NativeMethods.BluetoothGattFlags.None);
		if (result != 0) Marshal.ThrowExceptionForHR((int)result);
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
	private static unsafe void GattEventCallback(NativeMethods.BluetoothLeGattEventType eventType, void* eventData, void* context)
	{
		if (eventType == NativeMethods.BluetoothLeGattEventType.CharacteristicValueChangedEvent && GCHandle.FromIntPtr((nint)context).Target is { } registration)
		{
			ref var @event = ref *(NativeMethods.BluetoothLeGattCharacteristicValueChangedEvent*)eventData;
			Unsafe.As<CharacteristicChangedEventRegistration>(registration).HandleNotification
			(
				@event.ChangedAttributeHandle,
				new((byte*)@event.Value + 4, *(int*)@event.Value)
			);
		}
	}

	public static unsafe IDisposable RegisterValueChangedEvent
	(
		SafeFileHandle serviceHandle,
		in BluetoothLeCharacteristicInformation characteristic,
		BluetoothLeCharacteristicValueChangedHandler handler,
		object? state
	)
	{
		NativeMethods.BluetoothLeGattCharacteristicValueChangedEventRegistrationHeader details;
		details.CharacteristicCount = 1;
		details.FirstCharacteristic = Unsafe.As<BluetoothLeCharacteristicInformation, NativeMethods.BluetoothLeGattCharacteristic>(ref Unsafe.AsRef(in characteristic));
		return new CharacteristicChangedEventRegistration(serviceHandle, &details, handler, state);
	}

	//public static IDisposable RegisterValueChangedEvent
	//(
	//	SafeFileHandle serviceHandle,
	//	BluetoothLeCharacteristicInformation[] characteristic,
	//	BluetoothLeCharacteristicValueChangedHandler handler,
	//	object? state
	//)
	//{
	//	new CharacteristicChangedEventRegistration(serviceHandle, handler, state);
	//}

	internal sealed class CharacteristicChangedEventRegistration : IDisposable
	{
		private readonly BluetoothLeCharacteristicValueChangedHandler _handler;
		private readonly object? _state;
		private readonly nint _eventHandle;
		private readonly GCHandle _gcHandle;

		public unsafe CharacteristicChangedEventRegistration(SafeFileHandle serviceHandle, void* details, BluetoothLeCharacteristicValueChangedHandler handler, object? state)
		{
			_handler = handler;
			_state = state;
			// We use a weak GC handle because the object has to be held, and we want it to be finalizable.
			var gcHandle = GCHandle.Alloc(this, GCHandleType.Weak);
			uint result = NativeMethods.BluetoothGattRegisterEvent
			(
				serviceHandle,
				NativeMethods.BluetoothLeGattEventType.CharacteristicValueChangedEvent,
				details,
				&GattEventCallback,
				GCHandle.ToIntPtr(gcHandle),
				out _eventHandle,
				NativeMethods.BluetoothGattFlags.None
			);

			if (result != 0)
			{
				gcHandle.Free();
				Marshal.ThrowExceptionForHR((int)result);
			}
			_gcHandle = gcHandle;
		}

		~CharacteristicChangedEventRegistration() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			NativeMethods.BluetoothGattUnregisterEvent(_eventHandle, NativeMethods.BluetoothGattFlags.None);
			_gcHandle.Free();
		}

		public void HandleNotification(ushort attributeHandle, ReadOnlySpan<byte> data)
			=> _handler(new(attributeHandle), data, _state);
	}
}

public delegate void BluetoothLeCharacteristicValueChangedHandler(BluetoothLeHandle attribute, scoped ReadOnlySpan<byte> data, object? state);

public readonly struct BluetoothLeServiceInformation
{
	public BluetoothLeServiceInformation(BluetoothLeUuid uniqueId, BluetoothLeHandle handle)
	{
		UniqueId = uniqueId;
		Handle = handle;
	}

	public BluetoothLeUuid UniqueId { get; }
	public BluetoothLeHandle Handle { get; }
}

public readonly struct BluetoothLeHandle
{
	public readonly ushort Value;

	public BluetoothLeHandle(ushort value) => Value = value;
}

public readonly struct BluetoothLeUuid
{
	public BluetoothLeUuid(Guid longId) => LongId = longId;
	public BluetoothLeUuid(ushort shortId) => LongId = DeviceInterfaceClassGuids.GetBluetoothUuidGuid(shortId);

	public Guid LongId { get; }
	//public bool IsShortId => …;
	//public ushort ShortId => …;
	//public bool TryGetShortId(out ushort shortId) => …;
}

public readonly struct BluetoothLeCharacteristicInformation
{
	private readonly NativeMethods.BluetoothLeGattCharacteristic _wrappedCharacteristic;

	public readonly BluetoothLeUuid CharacteristicUuid
		=> _wrappedCharacteristic.CharacteristicUuid.IsShortUuid != 0 ?
			new BluetoothLeUuid(_wrappedCharacteristic.CharacteristicUuid.Value.ShortUuid) :
			new BluetoothLeUuid(_wrappedCharacteristic.CharacteristicUuid.Value.LongUuid);

	public readonly BluetoothLeHandle ServiceHandle => new(_wrappedCharacteristic.ServiceHandle);
	public readonly BluetoothLeHandle AttributeHandle => new(_wrappedCharacteristic.AttributeHandle);
	public readonly BluetoothLeHandle CharacteristicValueHandle => new(_wrappedCharacteristic.CharacteristicValueHandle);

	internal BluetoothLeCharacteristicInformation(NativeMethods.BluetoothLeGattCharacteristic wrappedCharacteristic)
		=> _wrappedCharacteristic = wrappedCharacteristic;

	public readonly bool IsBroadcastable => _wrappedCharacteristic.IsBroadcastable != 0;
	public readonly bool IsReadable => _wrappedCharacteristic.IsReadable != 0;
	public readonly bool IsWritable => _wrappedCharacteristic.IsWritable != 0;
	public readonly bool IsWritableWithoutResponse => _wrappedCharacteristic.IsWritableWithoutResponse != 0;
	public readonly bool IsSignedWritable => _wrappedCharacteristic.IsSignedWritable != 0;
	public readonly bool IsNotifiable => _wrappedCharacteristic.IsNotifiable != 0;
	public readonly bool IsIndicatable => _wrappedCharacteristic.IsIndicatable != 0;
	public readonly bool HasExtendedProperties => _wrappedCharacteristic.HasExtendedProperties != 0;
}

[Flags]
public enum BluetoothLeCharacteristicOptions : ushort
{
	None = 0x00,
	IsBroadcastable = 0x01,
	IsReadable = 0x02,
	IsWritable = 0x04,
	IsWritableWithoutResponse = 0x08,
	IsSignedWritable = 0x10,
	IsNotifiable = 0x20,
	IsIndicatable = 0x40,
	HasExtendedProperties = 0x80,
}
