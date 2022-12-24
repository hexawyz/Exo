using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace DeviceTools
{
	partial class NativeMethods
	{
		public enum DeviceObjectType
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

		[Flags]
		public enum DeviceQueryFlags
		{
			None = 0x0,
			UpdateResults = 0x1,
			AllProperties = 0x2,
			Localize = 0x4,
			AsyncClose = 0x8
		}

		public enum DeviceQueryState
		{
			DevQueryStateInitialized = 0,
			DevQueryStateEnumCompleted = 1,
			DevQueryStateAborted = 2,
			DevQueryStateClosed = 3,
		}

		public enum DeviceQueryResultAction
		{
			DevQueryResultStateChange = 0,
			DevQueryResultAdd = 1,
			DevQueryResultUpdate = 2,
			DevQueryResultRemove = 3,
		}

		public enum DevicePropertyStore
		{
			Sytem,
			User,
		}

		public enum DevPropertyOperator : uint
		{
			// operator modifiers
			ModifierNot = 0x00010000,
			ModifierIgnoreCase = 0x00020000,

			// comparison operators
			None = 0x00000000,
			Exists = 0x00000001,
			NotExists = Exists | ModifierNot,
			Equals = 0x00000002,
			NotEquals = Equals | ModifierNot,
			GreaterThan = 0x00000003,
			LessThan = 0x00000004,
			GreaterThanEquals = 0x00000005,
			LessThanEquals = 0x00000006,
			EqualsIgnoreCase = Equals | ModifierIgnoreCase,
			NotEqualsIgnoreCase = Equals | ModifierIgnoreCase | ModifierNot,
			BitwiseAnd = 0x00000007,
			BitwiseOr = 0x00000008,
			BeginsWith = 0x00000009,
			EndsWith = 0x0000000a,
			Contains = 0x0000000b,
			BeginsWithIgnoreCase = BeginsWith | ModifierIgnoreCase,
			EndsWithIgnoreCase = EndsWith | ModifierIgnoreCase,
			ContainsIgnoreCase = Contains | ModifierIgnoreCase,

			// list operators
			ListContains = 0x00001000,
			ListElementBeginsWith = 0x00002000,
			ListElementEndsWith = 0x00003000,
			ListElementContains = 0x00004000,
			ListContainsIgnoreCase = ListContains | ModifierIgnoreCase,
			ListElementBeginsWithIgnoreCase = ListElementBeginsWith | ModifierIgnoreCase,
			ListElementEndsWithIgnoreCase = ListElementEndsWith | ModifierIgnoreCase,
			ListElementContainsIgnoreCase = ListElementContains | ModifierIgnoreCase,

			// logical operators
			AndOpen = 0x00100000,
			AndClose = 0x00200000,
			OrOpen = 0x00300000,
			OrClose = 0x00400000,
			NotOpen = 0x00500000,
			NotClose = 0x00600000,

			// array operators
			ArrayContains = 0x10000000,

			// masks
			MaskEval = 0x00000FFF,
			MaskList = 0x0000F000,
			MaskModifier = 0x000F0000,
			MaskNotLogical = 0xF00FFFFF,
			MaskLogical = 0x0FF00000,
			MaskArray = 0xF0000000
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct DevicePropertyCompoundKey
		{
			public PropertyKey Key;
			public DevicePropertyStore Store;
			public char* LocaleName;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceProperty
		{
			public DevicePropertyCompoundKey CompoundKey;
			public DevicePropertyType Type;
			public uint BufferLength;
			public IntPtr Buffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DevicePropertyFilterExpression
		{
			public DevPropertyOperator Operator;
			public DeviceProperty Property;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceQueryParameter
		{
			public PropertyKey CompoundKey;
			public DevicePropertyType Type;
			public uint BufferLength;
			public IntPtr Buffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct DeviceObject
		{
			public DeviceObjectType ObjectType;
			public char* ObjectId;
			public uint PropertyCount;
			public DeviceProperty* Properties;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct DeviceQueryResultActionData
		{
			[FieldOffset(0)]
			public DeviceQueryResultAction Action;
			[FieldOffset(4)]
			public DeviceQueryState State;
			[FieldOffset(4)]
			public DeviceObject DeviceObject;
		}

#if !NET5_0_OR_GREATER
		public delegate void DeviceQueryCallback(SafeDeviceQueryHandle queryHandle, IntPtr context, in DeviceQueryResultActionData ActionData);
#endif

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQuery", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQuery
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQuery
#endif
		(
			DeviceObjectType objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQueryEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryEx
#endif
		(
			DeviceObjectType objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQueryFromId", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromId
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromId
#endif
		(
			DeviceObjectType objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQueryFromIdEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdEx
#endif
		(
			DeviceObjectType objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQueryFromId", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIds
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIds
#endif
		(
			DeviceObjectType objectType,
			ref char objectIds,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCreateObjectQueryFromIdsEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdsEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdsEx
#endif
		(
			DeviceObjectType objectType,
			ref char objectIds,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged<SafeDeviceQueryHandle, IntPtr, in DeviceQueryResultActionData, void> callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport("cfgmgr32", EntryPoint = "DevGetObjects", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceObject DeviceGetObjects
		(
			DeviceObjectType objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			out int objectCount
		);

		[DllImport("cfgmgr32", EntryPoint = "DevGetObjectsEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceObject DeviceGetObjectsEx
		(
			DeviceObjectType objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
			out int objectCount
		);

		[DllImport("cfgmgr32", EntryPoint = "DevGetObjectProperties", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceGetObjectProperties
		(
			DeviceObjectType objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			uint requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			out int propertyCount
		);

		[DllImport("cfgmgr32", EntryPoint = "DevGetObjectPropertiesEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceGetObjectPropertiesEx
		(
			DeviceObjectType objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
			out int propertyCount
		);

		[DllImport("cfgmgr32", EntryPoint = "DevFindProperty", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceFindProperty
		(
			in PropertyKey key,
			DevicePropertyStore store,
			ref char localeName,
			int propertyCount,
			ref DeviceProperty properties
		);

		[DllImport("cfgmgr32", EntryPoint = "DevCloseObjectQuery", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceCloseObjectQuery(IntPtr hDevQuery);

		[DllImport("cfgmgr32", EntryPoint = "DevFreeObjects", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceFreeObjects(int objectCount, ref DeviceObject deviceObjects);

		[DllImport("cfgmgr32", EntryPoint = "DevFreeObjectProperties", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceFreeObjectProperties(int propertyCount, ref DeviceProperty properties);
	}
}
