using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace DeviceTools.Bluetooth;

[SuppressUnmanagedCodeSecurity]
internal static class NativeMethods
{
	private const string BluetoothLibraryName = "BluetoothAPIs";

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct ShortOrLongUuid
	{
		[FieldOffset(0)]
		public readonly Guid LongUuid;
		[FieldOffset(0)]
		public readonly ushort ShortUuid;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeUuid
	{
		public readonly byte IsShortUuid;
		public readonly ShortOrLongUuid Value { get; }
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattService
	{
		public readonly BluetoothLeUuid ServiceUuid { get; }
		public readonly ushort AttributeHandle { get; }
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattCharacteristic
	{
		public readonly ushort ServiceHandle { get; }
		public readonly BluetoothLeUuid CharacteristicUuid { get; }
		public readonly ushort AttributeHandle { get; }
		public readonly ushort CharacteristicValueHandle { get; }
		public readonly byte IsBroadcastable { get; }
		public readonly byte IsReadable { get; }
		public readonly byte IsWritable { get; }
		public readonly byte IsWritableWithoutResponse { get; }
		public readonly byte IsSignedWritable { get; }
		public readonly byte IsNotifiable { get; }
		public readonly byte IsIndicatable { get; }
		public readonly byte HasExtendedProperties { get; }
	}

	public enum BluetoothLeGattDescriptorType
	{
		CharacteristicExtendedProperties,
		CharacteristicUserDescription,
		ClientCharacteristicConfiguration,
		ServerCharacteristicConfiguration,
		CharacteristicFormat,
		CharacteristicAggregateFormat,
		CustomDescriptor,
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattDescriptor
	{
		public readonly ushort ServiceHandle;
		public readonly ushort CharacteristicHandle;
		public readonly BluetoothLeGattDescriptorType DescriptorType;
		public readonly BluetoothLeUuid DescriptorUuid;
		public readonly ushort AttributeHandle;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattCharacteristicValueHeader
	{
		public readonly uint DataSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattCharacteristicValueChangedEventRegistrationHeader
	{
		public readonly uint DataSize;
	}

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct BluetoothLeGattDescriptorValueHeaderDetails
	{
		[FieldOffset(0)]
		public readonly CharacteristicExtendedProperties CharacteristicExtendedProperties;
		[FieldOffset(0)]
		public readonly ClientCharacteristicConfiguration ClientCharacteristicConfiguration;
		[FieldOffset(0)]
		public readonly ServerCharacteristicConfiguration ServerCharacteristicConfiguration;
		[FieldOffset(0)]
		public readonly CharacteristicFormat CharacteristicFormat;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct BluetoothLeGattDescriptorValueHeader
	{
		public readonly BluetoothLeGattDescriptorType DescriptorType;
		public readonly BluetoothLeUuid DescriptorUuid;

		public readonly BluetoothLeGattDescriptorValueHeaderDetails Details;

		public readonly uint DataSize;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct CharacteristicExtendedProperties
	{
		public readonly byte IsReliableWriteEnabled;
		public readonly byte IsAuxiliariesWritable;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct ClientCharacteristicConfiguration
	{
		public readonly byte IsSubscribeToNotification;
		public readonly byte IsSubscribeToIndication;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct ServerCharacteristicConfiguration
	{
		public readonly byte IsBroadcast;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct CharacteristicFormat
	{
		public readonly byte Format;
		public readonly byte Exponent;
		public readonly BluetoothLeUuid Unit;
		public readonly byte NameSpace;
		public readonly BluetoothLeUuid Description;
	}

	public enum BluetoothLeGattEventType
	{
		CharacteristicValueChangedEvent,
	}

	[Flags]
	public enum BluetoothGattFlags : uint
	{
		None = 0x00000000,
		ConnectionEncrypted = 0x00000001,
		ConnectionAuthenticated = 0x00000002,
		ForceReadFromDevice = 0x00000004,
		ForceReadFromCache = 0x00000008,
		SignedWrite = 0x00000010,
		WriteWithoutResponse = 0x00000020,
		ReturnAll = 0x00000040,
	}

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetServices", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetServices(SafeFileHandle deviceHandle, ushort bufferItemCount, BluetoothLeGattService* buffer, out ushort returnedItemCount, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetIncludedServices", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetIncludedServices(SafeFileHandle deviceHandle, BluetoothLeGattService* parentService, ushort bufferItemCount, BluetoothLeGattService* buffer, out ushort returnedItemCount, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetCharacteristics", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetCharacteristics(SafeFileHandle deviceHandle, BluetoothLeGattService* service, ushort bufferItemCount, BluetoothLeGattCharacteristic* buffer, out ushort returnedItemCount, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetCharacteristicValue", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetCharacteristicValue(SafeFileHandle deviceHandle, BluetoothLeGattCharacteristic* characteristic, ushort bufferSize, byte* buffer, out ushort returnedDataSize, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTSetCharacteristicValue", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattSetCharacteristicValue(SafeFileHandle deviceHandle, BluetoothLeGattCharacteristic* characteristic, byte* value, ulong writeContext, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetDescriptors", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetDescriptors(SafeFileHandle deviceHandle, BluetoothLeGattCharacteristic* characteristic, ushort bufferItemCount, BluetoothLeGattDescriptor* buffer, out ushort returnedItemCount, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTGetDescriptorValue", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattGetDescriptorValue(SafeFileHandle deviceHandle, BluetoothLeGattDescriptor* descriptor, ushort bufferSize, byte* buffer, out ushort returnedDataSize, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTSetDescriptorValue", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattSetDescriptorValue(SafeFileHandle deviceHandle, BluetoothLeGattDescriptor* descriptor, byte* value, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTRegisterEvent", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattRegisterEvent(SafeFileHandle deviceHandle, BluetoothLeGattEventType eventType, byte* registrationDetails, delegate* unmanaged[Stdcall]<BluetoothLeGattEventType, void*, void*, void> callback, void* callbackContext, out nint eventHandle, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTUnregisterEvent", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattUnregisterEvent(nint eventHandle, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTBeginReliableWrite", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattBeginReliableWrite(nint eventHandle, out ulong writeContext, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTEndReliableWrite", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattEndReliableWrite(nint eventHandle, ulong writeContext, BluetoothGattFlags flags);

	[DllImport(BluetoothLibraryName, EntryPoint = "BluetoothGATTAbortReliableWrite", ExactSpelling = true, SetLastError = false)]
	public static unsafe extern uint BluetoothGattAbortReliableWrite(nint eventHandle, ulong writeContext, BluetoothGattFlags flags);
}
