using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace DeviceTools
{
	partial class NativeMethods
	{
		// This is a struct we (have to) use to communicate with our native helper.
		// Apparently, for async queries, Cfgmgr32 likes to do a check on the HMODULE for some reason,
		// which for sadly obvious reasons, doesn't work for C# code that is JITted.
		// If I understand correctly, the DevQuery API will call GetModuleHandleExW to increment the reference count on the library,
		// then call FreeLibraryWhenCallbackReturns later on. The former is what is (sadly) causing the C# code to fail on its own.
		// This could probably be avoided if MS chanegd the way DevQuery worked by simply acknowledging callbacks not tied to a physical module,
		// however, as the status of the API is unclear, this might not be successful. (The API is available in the SDK, but undocumented. It is also available to Rust.)
		[StructLayout(LayoutKind.Sequential)]
#if NET5_0_OR_GREATER
		public unsafe struct DevQueryHelperContext
#else
		public struct DevQueryHelperContext
#endif
		{
#if NET5_0_OR_GREATER
			public delegate* unmanaged[Stdcall]<IntPtr, IntPtr, NativeMethods.DeviceQueryResultActionData*, void> Callback;
#else
			public DeviceQueryCallback Callback;
#endif
			public IntPtr Context;
		}

		// TODO: This shouldn't even be needed fot .NET Native. Inestigate how to conditionally avoid this mess.
		public static readonly IntPtr NativeDevQueryCallback = GetNativeDevQueryCallback();

		private static IntPtr GetNativeDevQueryCallback()
		{
			var directoryName = RuntimeInformation.ProcessArchitecture switch
			{
				Architecture.X86 => "x86",
				Architecture.X64 => "x64",
				Architecture.Arm => "arm32",
				Architecture.Arm64 => "arm64",
				_ => throw new NotSupportedException($"The architecture {RuntimeInformation.ProcessArchitecture} is currently not supported."),
			};

			string filename = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location)!, directoryName, "DeviceTools.DevQueryHelper.dll"));

#if NETCOREAPP3_0_OR_GREATER
			var module = NativeLibrary.Load(filename);
#else
			var module = LoadLibrary(filename);
#endif
			if (module == IntPtr.Zero)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

#if NETCOREAPP3_0_OR_GREATER
			var procAddress = NativeLibrary.GetExport(module, "DevQueryCallback");
#else
			var procAddress = GetProcAddress(module, "DevQueryCallback");
#endif
			if (procAddress == IntPtr.Zero)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error());
			}

			return procAddress;
		}

		private const string DevQueryLibrary = "api-ms-win-devices-query-l1-1-0";
		private const string DevQueryExLibrary = "api-ms-win-devices-query-l1-1-1";

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
			System,
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
			GreaterThanOrEquals = 0x00000005,
			LessThanOrEquals = 0x00000006,
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
			public PropertyKey Key;
			public DevicePropertyType Type;
			public uint BufferLength;
			public IntPtr Buffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct DeviceObject
		{
			public DeviceObjectKind ObjectType;
			public char* ObjectId;
			public uint PropertyCount;
			public DeviceProperty* Properties;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct DeviceQueryStateOrObject
		{
			[FieldOffset(0)]
			public DeviceQueryState State;
			[FieldOffset(0)]
			public DeviceObject DeviceObject;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct DeviceQueryResultActionData
		{
			public DeviceQueryResultAction Action;
			public DeviceQueryStateOrObject StateOrObject;
		}

#if !NET5_0_OR_GREATER
		public unsafe delegate void DeviceQueryCallback(IntPtr queryHandle, IntPtr context, DeviceQueryResultActionData* actionData);
#endif

		[DllImport(DevQueryLibrary, EntryPoint = "DevCreateObjectQuery", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQuery
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQuery
#endif
		(
			DeviceObjectKind objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryExLibrary, EntryPoint = "DevCreateObjectQueryEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryEx
#endif
		(
			DeviceObjectKind objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevCreateObjectQueryFromId", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromId
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromId
#endif
		(
			DeviceObjectKind objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryExLibrary, EntryPoint = "DevCreateObjectQueryFromIdEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdEx
#endif
		(
			DeviceObjectKind objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevCreateObjectQueryFromId", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIds
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIds
#endif
		(
			DeviceObjectKind objectType,
			ref char objectIds,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryExLibrary, EntryPoint = "DevCreateObjectQueryFromIdsEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
#if NET5_0_OR_GREATER
		public static unsafe extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdsEx
#else
		public static extern SafeDeviceQueryHandle DeviceCreateObjectQueryFromIdsEx
#endif
		(
			DeviceObjectKind objectType,
			ref char objectIds,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
#if NET5_0_OR_GREATER
			delegate* unmanaged[Stdcall]<IntPtr, IntPtr, DeviceQueryResultActionData*, void> callback,
#elif true
			IntPtr callback,
#else
			DeviceQueryCallback callback,
#endif
			IntPtr context
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevGetObjects", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceObject DeviceGetObjects
		(
			DeviceObjectKind objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			out int objectCount
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevGetObjectsEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceObject DeviceGetObjectsEx
		(
			DeviceObjectKind objectType,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int filterExpressionCount,
			ref DevicePropertyFilterExpression filters,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
			out int objectCount
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevGetObjectProperties", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceGetObjectProperties
		(
			DeviceObjectKind objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			uint requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			out int propertyCount
		);

		[DllImport(DevQueryExLibrary, EntryPoint = "DevGetObjectPropertiesEx", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceGetObjectPropertiesEx
		(
			DeviceObjectKind objectType,
			ref char objectId,
			DeviceQueryFlags queryFlags,
			int requestedPropertyCount,
			ref DevicePropertyCompoundKey requestedProperties,
			int extendedParameterCount,
			ref DeviceQueryParameter extendedParameters,
			out int propertyCount
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevFindProperty", PreserveSig = false, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern ref DeviceProperty DeviceFindProperty
		(
			in PropertyKey key,
			DevicePropertyStore store,
			ref char localeName,
			int propertyCount,
			ref DeviceProperty properties
		);

		[DllImport(DevQueryLibrary, EntryPoint = "DevCloseObjectQuery", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceCloseObjectQuery(IntPtr hDevQuery);

		[DllImport(DevQueryLibrary, EntryPoint = "DevFreeObjects", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceFreeObjects(int objectCount, ref DeviceObject deviceObjects);

		[DllImport(DevQueryLibrary, EntryPoint = "DevFreeObjectProperties", PreserveSig = true, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = false)]
		public static extern void DeviceFreeObjectProperties(int propertyCount, ref DeviceProperty properties);
	}
}
