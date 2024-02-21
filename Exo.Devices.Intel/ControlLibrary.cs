using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Devices.Intel;

#pragma warning disable IDE0044 // Add readonly modifier

internal unsafe sealed class ControlLibrary
{
	private const string LibraryName = "ControlLib";

	private static readonly ControlLibrary Instance = new();

	private static IntPtr Handle => Instance._handle;

	static ControlLibrary() { }

	private nint _handle;

	private ControlLibrary()
	{
		// NB: Request version 1.0 because version 1.1 can return version not supported, however it still doesn't work.
		var args = new InitArgs
		{
			Size = (uint)sizeof(InitArgs),
			AppVersion = 0x00010000,
			Flags = InitFlags.UseLevelZero,
		};

		nint handle = 0;

		ValidateResult(Init(&args, &handle));

		_handle = handle;
	}

	~ControlLibrary()
	{
		ValidateResult(Close(_handle));
	}

	public static DisplayAdapter[] GetDisplayAdapters()
	{
		uint deviceCount = 0;
		ValidateResult(EnumerateDevices(Handle, &deviceCount, null));
		return deviceCount <= 32 ? GetFromStack(deviceCount) : GetFromArray(deviceCount);

		static DisplayAdapter[] GetFromStack(uint count)
		{
			nint* handles = stackalloc nint[(int)count];
			ValidateResult(EnumerateDevices(Handle, &count, handles));
			return new Span<DisplayAdapter>((DisplayAdapter*)handles, (int)count).ToArray();
		}

		static DisplayAdapter[] GetFromArray(uint count)
		{
			var adapters = new DisplayAdapter[count];
			fixed (DisplayAdapter* adaptersPointer = adapters)
			{
				ValidateResult(EnumerateDevices(Handle, &count, (nint*)adaptersPointer));
			}
			return (uint)adapters.Length > count ? adapters[..(int)count] : adapters;
		}
	}

	public readonly struct DisplayAdapter
	{
		private readonly nint _handle;

		public bool IsValid => _handle != 0;

		public DisplayAdapterInformations GetInformations()
		{
			var properties = new DeviceAdapterProperties
			{
				Size = (uint)sizeof(DeviceAdapterProperties)
			};
			GetDeviceProperties(_handle, &properties);
			return new()
			{
				DeviceId = *properties.DeviceIdPointer,
				DeviceType = properties.DeviceType,
				SupportedFunctions = properties.SupportedFunctions,
				DriverVersion = properties.DriverVersion,
				FirmwareVersion = properties.FirmwareVersion,
				PciVendorId = properties.PciVendorId,
				PciDeviceId = properties.PciDeviceId,
				RevId = properties.RevId,
				SubSliceExecutionUnitCount = properties.SubSliceExecutionUnitCount,
				SliceSubSlicesCount = properties.SliceSubSlicesCount,
				SliceCount = properties.SliceCount,
				DeviceName = properties.GetName(),
				GraphicsAdapterProperties = properties.GraphicsAdapterProperties,
				Frequency = properties.Frequency,
				PciSubsystemId = properties.PciSubsystemId,
				PciSubsystemVendorId = properties.PciSubsystemVendorId,
				BusNumber = properties.AdapterBusDeviceFunction.Bus,
				DeviceIndex = properties.AdapterBusDeviceFunction.Device,
				FunctionId = properties.AdapterBusDeviceFunction.Function,
			};
		}
	}

	public readonly struct DisplayAdapterInformations
	{
		public Luid DeviceId { get; init; }
		public DeviceType DeviceType { get; init; }
		public SupportedFunctions SupportedFunctions { get; init; }
		public ulong DriverVersion { get; init; }
		public FirmwareVersion FirmwareVersion { get; init; }
		public uint PciVendorId { get; init; }
		public uint PciDeviceId { get; init; }
		public uint RevId { get; init; }
		public uint SubSliceExecutionUnitCount { get; init; }
		public uint SliceSubSlicesCount { get; init; }
		public uint SliceCount { get; init; }
		public string DeviceName { get; init; }
		public AdapterProperties GraphicsAdapterProperties { get; init; }
		public uint Frequency { get; init; }
		public ushort PciSubsystemId { get; init; }
		public ushort PciSubsystemVendorId { get; init; }
		public byte BusNumber { get; init; }
		public byte DeviceIndex { get; init; }
		public byte FunctionId { get; init; }
	}

	private enum Result
	{
		Success = 0x00000000,
		SuccessStillOpenByAnotherCaller = 0x00000001,

		ErrorNotInitialized = 0x40000001,
		ErrorAlreadyInitialized = 0x40000002,
		ErrorDeviceLost = 0x40000003,
		ErrorOutOfHostMemory = 0x40000004,
		ErrorOutOfDeviceMemory = 0x40000005,
		ErrorInsufficientPermissions = 0x40000006,
		ErrorNotAvailable = 0x40000007,
		ErrorUninitialized = 0x40000008,
		ErrorUnsupportedVersion = 0x40000009,
		ErrorUnsupportedFeature = 0x4000000a,
		ErrorInvalidArgument = 0x4000000b,
		ErrorInvalidApiHandle = 0x4000000c,
		ErrorInvalidNullHandle = 0x4000000d,
		ErrorInvalidNullPointer = 0x4000000e,
		ErrorInvalidSize = 0x4000000f,
		ErrorUnsupportedSize = 0x40000010,
		ErrorUnsupportedImageFormat = 0x40000011,
		ErrorDataRead = 0x40000012,
		ErrorDataWrite = 0x40000013,
		ErrorDataNotFound = 0x40000014,
		ErrorNotImplemented = 0x40000015,
		ErrorOsCall = 0x40000016,
		ErrorKernelModeDriverCall = 0x40000017,
		ErrorUnload = 0x40000018,
		ErrorLevelZeroLoader = 0x40000019,
		ErrorInvalidOperationType = 0x4000001a,
		ErrorNullOsInterface = 0x4000001b,
		ErrorNullOsAdapterHandle = 0x4000001c,
		ErrorNullOsDisplayOutputHandle = 0x4000001d,
		ErrorWaitTimeout = 0x4000001e,
		ErrorPersistanceNotSupported = 0x4000001f,
		ErrorPlatformNotSupported = 0x40000020,
		ErrorUnknownApplicationUid = 0x40000021,
		ErrorInvalidEnumeration = 0x40000022,
		ErrorFileDelete = 0x40000023,
		ErrorResetDeviceRequired = 0x40000024,
		ErrorFullRebootRequired = 0x40000025,
		ErrorLoad = 0x40000026,
		ErrorUnknown = 0x4000FFFF,
		ErrorRetryOperation = 0x40010000,

		ErrorCoreOverclockNotSupported = 0x44000001,
		ErrorCoreOverclockVoltageOutsideRange = 0x44000002,
		ErrorCoreOverclockFrequencyOutsideRange = 0x44000003,
		ErrorCoreOverclockPowerOutsideRange = 0x44000004,
		ErrorCoreOverclockTemperatureOutsideRange = 0x44000005,
		ErrorCoreOverclockInVoltageLockedMode = 0x44000006,
		ErrorCoreOverclockResetRequired = 0x44000007,
		ErrorCoreOverclockWaiverNotSet = 0x44000008,

		ErrorInvalidAuxAccessFlag = 0x48000001,
		ErrorInvalidSharpnessFilterFlag = 0x48000002,
		ErrorDisplayNotAttached = 0x48000003,
		ErrorDisplayNotActive = 0x48000004,
		ErrorInvalidPowerFeatureOptimizationFlag = 0x48000005,
		ErrorInvalidPowerSourceTypeForDisplayPowerSavingTechnology = 0x48000006,
		ErrorInvalidPixelTransformationGetConfigQueryType = 0x48000007,
		ErrorInvalidPixelTransformationSetConfigOperationType = 0x48000008,
		ErrorInvalidSetConfigNumberOfSamples = 0x48000009,
		ErrorInvalidPixelTransformationBlockId = 0x4800000a,
		ErrorInvalidPixelTransformationBlockType = 0x4800000b,
		ErrorInvalidPixelTransformationBlockNumber = 0x4800000c,
		ErrorInsufficientPixelTransformationBlockConfigMemory = 0x4800000d,
		Error3DLookupTableInvalidPipe = 0x4800000e,
		Error3DLookupTableInvalidData = 0x4800000f,
		Error3DLookupTableNotSupportedInHdr = 0x48000010,
		Error3DLookupTableInvalidOperation = 0x48000011,
		Error3DLookupTableUnsuccessful = 0x48000012,
		ErrorAuxDefer = 0x48000013,
		ErrorAuxTimeout = 0x48000014,
		ErrorAuxIncompleteWrite = 0x48000015,
		ErrorI2cAuxStatusUnknown = 0x48000016,
		ErrorI2cAuxUnsuccessful = 0x48000017,
		ErrorLocalizedAdaptiveContrastEnhancementInvalidDataArgumentPassed = 0x48000018,
		ErrorExternalDisplayAttached = 0x48000019,
		ErrorCustomModeStandardCustomModeExists = 0x4800001a,
		ErrorCustomModeNonCustomMatchingModeExists = 0x4800001b,
		ErrorCustomModeInsufficientMemory = 0x4800001c,
		ErrorAdapterAlreadyLinked = 0x4800001d,
		ErrorAdapterNotIdentical = 0x4800001e,
		ErrorAdapterNotSupportedOnLinkedDisplayAdapterSecondary = 0x4800001f,
		ErrorSetFbcFeatureNotSupported = 0x48000020,
	}

	private static void ValidateResult(Result result)
	{
		if (result == Result.Success) return;
		switch (result)
		{
		case Result.ErrorPlatformNotSupported: throw new PlatformNotSupportedException();
		case Result.ErrorInvalidArgument: throw new ArgumentException();
		case Result.ErrorInvalidNullPointer: throw new ArgumentNullException();
		case Result.ErrorInvalidOperationType: throw new InvalidOperationException();
		case Result.ErrorOutOfHostMemory: throw new OutOfMemoryException();
		}
		throw new InvalidOperationException(result.ToString());
	}

	private enum InitFlags
	{
		None = 0x0000,
		UseLevelZero = 0x0001,
	}

	private struct InitArgs
	{
		public uint Size;
		public byte Version;
		public uint AppVersion;
		public InitFlags Flags;
		public uint SupportedVersion;
		public Guid ApplicationUid;
	}

	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Luid : IEquatable<Luid>
	{
		public Luid(long value)
			=> (LowPart, HighPart) = ((uint)value, (int)(value >> 32));

		public readonly uint LowPart;
		public readonly int HighPart;

		public long ToInt64() => (long)HighPart << 32 | LowPart;

		public override bool Equals(object? obj) => obj is Luid luid && Equals(luid);
		public bool Equals(Luid other) => LowPart == other.LowPart && HighPart == other.HighPart;
		public override int GetHashCode() => HashCode.Combine(LowPart, HighPart);

		public static bool operator ==(Luid left, Luid right) => left.Equals(right);
		public static bool operator !=(Luid left, Luid right) => !(left == right);
	}

	[InlineArray(112)]
	private struct ByteArray112
	{
		private byte _element0;
	}

	[InlineArray(100)]
	private struct ByteArray100
	{
		private byte _element0;
	}

	public enum DeviceType
	{
		Graphics = 1,
		System = 2,
	}

	[Flags]
	public enum SupportedFunctions
	{
		None = 0x0000,
		Display = 0x0001,
		ThreeDimensional = 0x0002,
		Media = 0x0004,
	}

	[Flags]
	public enum AdapterProperties
	{
		None = 0x0000,
		Integrated = 0x0001,
		LinkedDisplayAdapterPrimary = 0x0002,
		LinkedDisplayAdapterSecondary = 0x0004,
	}

	public struct FirmwareVersion
	{
		public ulong Major;
		public ulong Minor;
		public ulong Build;
	}

	private struct AdapterBusDeviceFunction
	{
		public byte Bus;
		public byte Device;
		public byte Function;
	}

	private struct DeviceAdapterProperties
	{
		public uint Size;
		public byte Version;
		public Luid* DeviceIdPointer;
		public uint DeviceIdSize;
		public DeviceType DeviceType;
		public SupportedFunctions SupportedFunctions;
		public ulong DriverVersion;
		public FirmwareVersion FirmwareVersion;
		public uint PciVendorId;
		public uint PciDeviceId;
		public uint RevId;
		public uint SubSliceExecutionUnitCount;
		public uint SliceSubSlicesCount;
		public uint SliceCount;
		private ByteArray100 _name;
		public AdapterProperties GraphicsAdapterProperties;
		public uint Frequency;
		public ushort PciSubsystemId;
		public ushort PciSubsystemVendorId;
		public AdapterBusDeviceFunction AdapterBusDeviceFunction;
		private ByteArray112 _reserved;

		public string GetName()
		{
			var name = (ReadOnlySpan<byte>)_name;

			int endIndex = name.IndexOf((byte)0);
			if (endIndex < 0) endIndex = name.Length;

			return Encoding.UTF8.GetString(name[..endIndex]);
		}
	}

	[DllImport(LibraryName, EntryPoint = "ctlInit", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	private static extern Result Init(InitArgs* initDescriptor, nint* apiHandle);

	[DllImport(LibraryName, EntryPoint = "ctlClose", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	private static extern Result Close(nint apiHandle);

	[DllImport(LibraryName, EntryPoint = "ctlEnumerateDevices", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	private static extern Result EnumerateDevices(nint apiHandle, uint* deviceCount, nint* deviceHandles);

	[DllImport(LibraryName, EntryPoint = "ctlGetDeviceProperties", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	private static extern Result GetDeviceProperties(nint deviceAdapterHandle, DeviceAdapterProperties* properties);
}
