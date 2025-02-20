using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace Exo.Devices.NVidia;

#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable CS0649
#pragma warning disable CS0169

internal sealed class NvApi
{
	private static readonly NvApi Instance = new();
	public static readonly uint DriverVersion;
	public static readonly string DriverBranch;

	public static bool HasI2c => DriverVersion >= 85_00;
	public static bool HasThermals => DriverVersion >= 85_00;
	public static bool HasClockFrequencies => DriverVersion >= 285_00;
	public static bool HasIllumination => DriverVersion >= 400_00;
	public static bool HasUtilizationSamples => DriverVersion >= 455_00;

	static NvApi()
	{
		(DriverVersion, DriverBranch) = System.GetDriverAndBranchVersion();
	}

	private unsafe NvApi()
	{
		Functions.Initialize();
	}

	unsafe ~NvApi()
	{
		Functions.Unload();
	}

	// Define and load all the function pointers that we are gonna need.
	private static unsafe class Functions
	{
		public static readonly delegate* unmanaged[Cdecl]<uint> Initialize = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x0150e828);
		public static readonly delegate* unmanaged[Cdecl]<uint> Unload = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xd22bdd7e);
		public static readonly delegate* unmanaged[Cdecl]<uint, ShortString*, uint> GetErrorMessage = (delegate* unmanaged[Cdecl]<uint, ShortString*, uint>)QueryInterface(0x6c2d048c);
		public static readonly delegate* unmanaged[Cdecl]<ShortString*, uint> GetInterfaceVersionString = (delegate* unmanaged[Cdecl]<ShortString*, uint>)QueryInterface(0x01053fa5);
		public static readonly delegate* unmanaged[Cdecl]<nint*, int*, uint> EnumPhysicalGPUs = (delegate* unmanaged[Cdecl]<nint*, int*, uint>)QueryInterface(0xe5ac921f);
		// NB: The …Ex versions of I2C APIs are not officially documented but are easily found all over internet.
		// I'm assuming those can be used to bypass some of the forced data processing of the regular versions.
		// Notably, it allows passing bIsDDCPort as false without returning an error, but it stills seems to process the data. (Need to find the right parameters to use)
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint> I2CRead = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint>)QueryInterface(0x2fde12c5);
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint*, uint> I2CReadEx = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint*, uint>)QueryInterface(0x4d7b0709);
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint> I2CWrite = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint>)QueryInterface(0xe812eb07);
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint*, uint> I2CWriteEx = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint*, uint>)QueryInterface(0x283ac65a);

		public static class Gpu
		{
			public static readonly delegate* unmanaged[Cdecl]<nint, uint, Edid*, uint> GetEdid = (delegate* unmanaged[Cdecl]<nint, uint, Edid*, uint>)QueryInterface(0x37d32e69);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.BoardInfo*, uint> GetBoardInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.BoardInfo*, uint>)QueryInterface(0x22d54523);
			// See: https://github.com/graphitemaster/NVFC/blob/master/src/nvapi.h for the serial number stuff.
			public static readonly delegate* unmanaged[Cdecl]<nint, ByteArray64*, uint> GetSerialNumber = (delegate* unmanaged[Cdecl]<nint, ByteArray64*, uint>)QueryInterface(0x014b83a5f);
			public static readonly delegate* unmanaged[Cdecl]<nint, ShortString*, uint> GetFullName = (delegate* unmanaged[Cdecl]<nint, ShortString*, uint>)QueryInterface(0xceee8e9f);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x1be0b8e5);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusSlotId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x2a0a350f);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.DisplayIdInfo*, uint*, NvApi.Gpu.ConnectedIdFlags, uint> GetConnectedDisplayIds = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.DisplayIdInfo*, uint*, NvApi.Gpu.ConnectedIdFlags, uint>)QueryInterface(0x0078dba2);
			// Found about this here: https://www.cnblogs.com/zzz3265/p/16517057.html (NvAPI_GPU_Get_I2C_Ports_Info)
			// The API seems mostly straightforward, but I'm yet unsure how to exploit the results. (Here it lists ports with an info value of either 100 or 400. Could be the speed?)
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.I2cPortInfo*, uint, uint> GetI2cPortsInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.I2cPortInfo*, uint, uint>)QueryInterface(0x5E4B36C3);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> QueryIlluminationSupport = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0xa629da31);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> GetIllumination = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0x9a1b9365);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> SetIllumination = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0x0254a187);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceInfoQuery*, uint> ClientIllumDevicesGetInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceInfoQuery*, uint>)QueryInterface(0xd4100e58);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint> ClientIllumDevicesGetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint>)QueryInterface(0x73c01d58);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint> ClientIllumDevicesSetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint>)QueryInterface(0x57024c62);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneInfoQuery*, uint> ClientIllumZonesGetInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneInfoQuery*, uint>)QueryInterface(0x4b81241b);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint> ClientIllumZonesGetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint>)QueryInterface(0x3dbf5764);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint> ClientIllumZonesSetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint>)QueryInterface(0x197d065e);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.UtilizationPeriodicCallbackSettings*, uint> ClientRegisterForUtilizationSampleUpdates = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.UtilizationPeriodicCallbackSettings*, uint>)QueryInterface(0xadeeaf67);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.ThermalSettings*, uint> GetThermalSettings = (delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.ThermalSettings*, uint>)QueryInterface(0xe3640a56);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.ClockFrequencies*, uint> GetAllClockFrequencies = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.ClockFrequencies*, uint>)QueryInterface(0xdcb616c3);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetTachReading = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x5f608315);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.CoolerSettings*, uint> GetCoolerSettings = (delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.CoolerSettings*, uint>)QueryInterface(0xda141340);
			//public static readonly delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.CoolerSettings*, uint> ClientFanCoolersGetInfo = (delegate* unmanaged[Cdecl]<nint, uint, NvApi.Gpu.CoolerSettings*, uint>)QueryInterface(0xfb85b01e);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersStatus*, uint> ClientFanCoolersGetStatus = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersStatus*, uint>)QueryInterface(0x35aed5e8);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersControl*, uint> ClientFanCoolersGetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersControl*, uint>)QueryInterface(0x814b209f);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersControl*, uint> ClientFanCoolersSetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.FanCoolersControl*, uint>)QueryInterface(0xa58971a5);
		}

		public static class System
		{
			public static readonly delegate* unmanaged[Cdecl]<uint*, ShortString*, uint> GetDriverAndBranchVersion = (delegate* unmanaged[Cdecl]<uint*, ShortString*, uint>)QueryInterface(0x2926aaad);
			public static readonly delegate* unmanaged[Cdecl]<uint, nint*, uint*, uint> GetGpuAndOutputIdFromDisplayId = (delegate* unmanaged[Cdecl]<uint, nint*, uint*, uint>)QueryInterface(0x112ba1a5);
		}
	}

	internal struct ShortString
	{
		private long _0;
		private long _1;
		private long _2;
		private long _3;
		private long _4;
		private long _5;
		private long _6;
		private long _7;

		public static Span<byte> GetSpan(ref ShortString shortString)
			=> MemoryMarshal.CreateSpan(ref Unsafe.As<long, byte>(ref shortString._0), 64);

		public readonly override string ToString()
		{
			var span = GetSpan(ref Unsafe.AsRef(in this));

			int end = span.IndexOf((byte)0);

			if (end < 0) end = span.Length;

			return Encoding.UTF8.GetString(span[..end]);
		}
	}

	private static unsafe uint StructVersion<T>(int version)
		where T : unmanaged
		=> (uint)(sizeof(T) | version << 16);

	[InlineArray(61)]
	private struct ByteArray61
	{
		private byte _element0;
	}

	[InlineArray(64)]
	private struct ByteArray64
	{
		private byte _element0;
	}

	[InlineArray(256)]
	private struct ByteArray256
	{
		private byte _element0;
	}

	public enum MonitorConnectorType
	{
		Unknown = -1,
		Uninitialized = 0,
		Vga,
		Component,
		SVideo,
		Hdmi,
		Dvi,
		Lvds,
		DisplayPort,
		Composite,
	}

	private struct Edid
	{
		public uint Version;
		public ByteArray256 Data;
		public uint EdidSize;
		public uint EdidId;
		public uint Offset;
	}

	public enum I2CSpeed
	{
		Default,
		Frequency3Khz,
		Frequency10Khz,
		Frequency33Khz,
		Frequency100Khz,
		Frequency200Khz,
		Frequency400Khz,
	}

	private unsafe struct I2CInfo
	{
		public uint Version;
		public uint DisplayMask;
		private byte _isDdcPort;
		public byte DeviceAddress;
		public byte* RegisterAddress;
		public uint RegisterAddressSize;
		public byte* Data;
		public uint Size;
		public uint DeprecatedSpeed;
		public I2CSpeed Speed;
		public byte PortId;
		private uint _isPortIdSet;

		public bool IsDdcPort
		{
			get => _isDdcPort != 0;
			set => _isDdcPort = value ? (byte)1 : (byte)0;
		}

		public bool IsPortIdSet
		{
			get => _isPortIdSet != 0;
			set => _isPortIdSet = value ? 1U : 0;
		}
	}

	//public enum EdidFlag
	//{
	//	Default = 0,
	//	Raw = 1,
	//	Cooked = 2,
	//	Forced = 3,
	//	Inf = 4,
	//	Hardware = 5,
	//}

	public static class Gpu
	{
		internal struct BoardNumber
		{
			private byte _0;
			private byte _1;
			private byte _2;
			private byte _3;
			private byte _4;
			private byte _5;
			private byte _6;
			private byte _7;
			private byte _8;
			private byte _9;
			private byte _a;
			private byte _b;
			private byte _c;
			private byte _d;
			private byte _e;
			private byte _f;

			public static Span<byte> GetSpan(ref BoardNumber boardNumber)
				=> MemoryMarshal.CreateSpan(ref boardNumber._0, 16);

			public byte[] ToByteArray() => GetSpan(ref Unsafe.AsRef(in this)).ToArray();

			public override string ToString() => Convert.ToHexString(GetSpan(ref Unsafe.AsRef(in this)));
		}

		internal struct BoardInfo
		{
			public uint Version;
			public BoardNumber BoardNumber;
		}

		public enum ConnectedIdFlags : uint
		{
			None = 0x0000,
			Uncached = 0x0001,
			Sli = 0x0002,
			LidState = 0x0004,
			Fake = 0x0008,
			ExcludeMultiStream = 0x0010,
		}

		public enum DisplayFlags : uint
		{
			None = 0x0000,
			Dynamic = 0x0001,
			MultiStreamRootNode = 0x0002,
			Active = 0x0004,
			Cluster = 0x0008,
			OsVisible = 0x0010,
			//Wfd = 0x0020,
			Connected = 0x0040,
			PhysicallyConnected = 0x00020000,
		}

		public struct DisplayIdInfo
		{
			public uint Version;
			public MonitorConnectorType ConnectorType;
			public uint DisplayId;
			public DisplayFlags Flags;
		}

		internal struct I2cPortInfo
		{
			public uint Version;
			public uint Index;
			public uint Data;
		}

		public enum IlluminationZone : int
		{
			Logo,
			Sli,
		}

		internal enum IlluminationAttribute : int
		{
			LogoBrightness,
			SliBrightness,
		}

		// This fuses the Query, Get and Set structures, as they currently use the same API.
		// Query returns a boolean, and Get/Set expose the brightness.
		internal struct IlluminationQuery
		{
			public uint Version;
			public nint PhysicalGpuHandle;
			public IlluminationAttribute Attribute;
			public uint Value;
		}

		public enum PublicClock
		{
			Graphics = 0,
			Memory = 4,
			Processor = 7,
			Video = 8,
		}

		public enum ClockType
		{
			Current = 0,
			Base = 1,
			Boost = 2,
		}

		public struct ClockFrequency
		{
			private uint _flags;
			public uint FrequencyInKiloHertz;

			public bool IsPresent => (_flags & 1) != 0;
		}

		[InlineArray(32)]
		internal struct ClockFrequencyArray
		{
			private ClockFrequency _element0;
		}

		public struct ClockFrequencies
		{
			public uint Version;
			private uint _flags;
			public ClockType ClockType
			{
				get => (ClockType)(_flags & 0xFU);
				set => _flags = (_flags & ~0xFU) | ((uint)value & 0xFU);
			}
			public ClockFrequencyArray Domains;
		}

		public enum ThermalController
		{
			None = 0,
			GpuInternal,
			Adm1032,
			Max6649,
			Max1617,
			Lm99,
			Lm89,
			Lm64,
			Adt7473,
			SBMAX6649,
			VideoBiosEvent,
			Os,
			Unknown = -1,
		}

		public enum ThermalTarget
		{
			None = 0,
			Gpu = 1,
			Memory = 2,
			PowerSupply = 4,
			Board = 8,
			VisualComputingDeviceBoard = 9,
			VisualComputingDeviceInlet = 10,
			VisualComputingDeviceOutlet = 11,
			All = 15,
			Unknown = -1,
		}

		internal struct ThermalSensor
		{
			public ThermalController Controller;
			public int DefaultMinTemp;
			public int DefaultMaxTemp;
			public int CurrentTemp;
			public ThermalTarget Target;
		}

		[InlineArray(3)]
		internal struct ThermalSensorArray
		{
			private ThermalSensor _element0;
		}

		internal struct ThermalSettings
		{
			public uint Version;
			public uint Count;
			public ThermalSensorArray Sensors;
		}

		internal const int ThermalSettingsVersion = 2;

		public enum CoolerType
		{
			None = 0,
			Fan = 1,
			Water = 2,
			LiquidNo2 = 3,
		}

		public enum CoolerController
		{
			None = 0,
			Adi = 1,
			Internal = 2,
		}

		[Flags]
		public enum CoolerPolicy
		{
			None = 0,
			Manual = 1,
			Performance = 2,
			TemperatureDiscrete = 4,
			TemperatureContinuous = 8,
			Hybrid = 9,
		}

		[Flags]
		public enum CoolerTarget
		{
			None = 0,
			Gpu = 1,
			Memory = 2,
			PowerSupply = 4,
			All = 7,
		}

		public enum CoolerControl
		{
			None = 0,
			Toggle = 1,
			Variable = 2,
		}

		public enum CoolerActivityLevel
		{
			Inactive = 0,
			Active = 1,
		}

		internal struct CoolerTachometer
		{
			public uint CurrentSpeedInRotationsPerMinute;
			private byte _isSupported;
			public bool IsSupported => _isSupported != 0;
			public uint MaximumSpeedInRotationsPerMinute;
			public uint MinimumSpeedInRotationsPerMinute;
		}

		// See: https://www.autohotkey.com/boards/viewtopic.php?t=4469&p=32783
		internal struct CoolerInformation
		{
			public CoolerType Type;
			public CoolerController Controller;
			public uint DefaultMinimumLevel;
			public uint DefaultMaximumLevel;
			public uint CurrentMinimumLevel;
			public uint CurrentMaximumLevel;
			public uint CurrentLevel;
			public CoolerPolicy DefaultPolicy;
			public CoolerPolicy CurrentPolicy;
			public CoolerTarget Target;
			public CoolerControl Control;
			public CoolerActivityLevel ActivityLevel;
			public CoolerTachometer Tachometer; // Added in V3 (Size 0x508)
			public uint Unknown; // Added in V4 (Size 0x558)
		}

		[InlineArray(20)]
		internal struct CoolerInfoArray
		{
			private CoolerInformation _element0;
		}

		internal struct CoolerSettings
		{
			public uint Version;
			public uint Count;
			public CoolerInfoArray Coolers;
		}

		internal const int CoolerSettingsVersion = 4;

		public static class Client
		{
			public enum IlluminationDeviceType : int
			{
				Invalid = 0,
				McuV10 = 1,
				GpioPwmRgbw = 2,
				GpioPwmSingleColor = 3,
			}

			public struct IlluminationDeviceInfoDataMcuV10
			{
				public byte I2CDeviceIndex;
			}

			public struct IlluminationDeviceInfoDataGpioPwmRgbw
			{
				public byte GpioPinRed;
				public byte GpioPinGreen;
				public byte GpioPinBlue;
				public byte GpioPinWhite;
			}

			public struct IlluminationDeviceInfoDataGpioPwmSingleColor
			{
				public byte GpioPinSingleColor;
			}

			[StructLayout(LayoutKind.Explicit)]
			public struct IlluminationDeviceInfoData
			{
				[FieldOffset(0)]
				public IlluminationDeviceInfoDataMcuV10 McuV10;
				[FieldOffset(0)]
				public IlluminationDeviceInfoDataGpioPwmRgbw GpioPwmRgbwv10;
				[FieldOffset(0)]
				public IlluminationDeviceInfoDataGpioPwmSingleColor GpioPwmSingleColorv10;
				[FieldOffset(0)]
				private ByteArray64 _reserved;
			}

			public struct IlluminationDeviceInfo
			{
				public IlluminationDeviceType DeviceType;
				public uint SupportedControlModes;
				public IlluminationDeviceInfoData Data;
				private ByteArray64 _reserved;
			}

			[InlineArray(32)]
			internal struct IlluminationDeviceInfoArray
			{
				private IlluminationDeviceInfo _element0;
			}

			internal struct IlluminationDeviceInfoQuery
			{
				public uint Version;
				public int DeviceCount;
				private readonly ByteArray64 _reserved;
				public IlluminationDeviceInfoArray Devices;
			}

			public struct IlluminationDeviceSync
			{
				public byte NeedsSynchronization;
				public ulong TimeStampInMilliseconds;
				private readonly ByteArray64 _reserved;
			}

			public struct IlluminationDeviceControl
			{
				public IlluminationDeviceType Type;
				public IlluminationDeviceSync SynchronizationData;
				private readonly ByteArray64 _reserved;
			}

			[InlineArray(32)]
			public struct IlluminationDeviceControlArray
			{
				private IlluminationDeviceControl _element0;
			}

			internal struct IlluminationDeviceControlQuery
			{
				public uint Version;
				public int DeviceCount;
				private readonly ByteArray64 _reserved;
				public IlluminationDeviceControlArray Devices;
			}

			public enum IlluminationZoneLocationFace : sbyte
			{
				Invalid = -1,
				Top = 0,
				Front = 2,
				Back = 3,
			}

			public enum IlluminationZoneLocationComponent : byte
			{
				Gpu = 0,
				Sli = 1,
			}

			public enum IlluminationZoneType : int
			{
				Invalid = 0,
				Rgb = 1,
				ColorFixed = 2,
				Rgbw = 3,
				SingleColor = 4,
			}

			public enum IlluminationControlMode : int
			{
				Manual = 0,
				PieceWiseLinear = 1,

				Invalid = 255,
			}

			public enum IlluminationPiecewiseLinearCycleType : int
			{
				HalfHalt = 0,
				FullHalt = 1,
				FullRepeat = 2,

				Invalid = 255,
			}

			public readonly struct IlluminationZoneLocation
			{
				private readonly uint _value;

				public byte Index => (byte)(_value & 0x03);
				public IlluminationZoneLocationFace Face => (IlluminationZoneLocationFace)((_value >>> 2) & 0x7);
				public IlluminationZoneLocationComponent Component => (IlluminationZoneLocationComponent)((_value >>> 6) & 0x3);

				private readonly ByteArray64 _reserved;
			}

			public struct IlluminationZoneInfoDataRgb
			{
				private byte _reserved;
			}

			public struct IlluminationZoneInfoDataRgbw
			{
				private byte _reserved;
			}

			public struct IlluminationZoneInfoDataSingleColor
			{
				private byte _reserved;
			}

			[StructLayout(LayoutKind.Explicit)]
			public struct IlluminationZoneInfoData
			{
				[FieldOffset(0)]
				public IlluminationZoneInfoDataRgb Rgb;
				[FieldOffset(0)]
				public IlluminationZoneInfoDataRgbw Rgbw;
				[FieldOffset(0)]
				public IlluminationZoneInfoDataSingleColor SingleColor;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved;
			}

			// The fixed alignment seems to be correct, but it differs from the auto alignment that would be generated by .NET
			[StructLayout(LayoutKind.Explicit)]
			public struct IlluminationZoneInfo
			{
				[FieldOffset(0)]
				public IlluminationZoneType Type;
				[FieldOffset(4)]
				public byte DeviceIndex;
				[FieldOffset(5)]
				public byte ProviderIndex;
				[FieldOffset(8)]
				public IlluminationZoneLocation Location;
				[FieldOffset(12)]
				public IlluminationZoneInfoData Data;
				[FieldOffset(76)]
				private readonly ByteArray64 _reserved;
			}

			[InlineArray(32)]
			public struct IlluminationZoneInfoArray
			{
				private IlluminationZoneInfo _element0;
			}

			internal struct IlluminationZoneInfoQuery
			{
				public uint Version;
				public int ZoneCount;
				private readonly ByteArray64 _reserved;
				public IlluminationZoneInfoArray Zones;
			}

			public struct IlluminationZoneControlDataPiecewiseLinear
			{
				public IlluminationPiecewiseLinearCycleType CycleType;
				public byte GroupCount;
				public ushort RiseTimeInMilliseconds;
				public ushort FallTimeInMilliseconds;
				public ushort ColorATimeInMilliseconds;
				public ushort ColorBTimeInMilliseconds;
				public ushort GroupIdleTimeInMilliseconds;
				public ushort PhaseOffsetInMilliseconds;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct IlluminationZoneControlDataManualRgb
			{
				public byte R;
				public byte G;
				public byte B;
				public byte BrightnessPercentage;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct IlluminationZoneControlDataManualRgbw
			{
				public byte R;
				public byte G;
				public byte B;
				public byte W;
				public byte BrightnessPercentage;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct IlluminationZoneControlDataManualColorFixed
			{
				public byte BrightnessPercentage;
			}

			[StructLayout(LayoutKind.Sequential, Pack = 1)]
			public struct IlluminationZoneControlDataManualSingleColor
			{
				public byte BrightnessPercentage;
			}

			public struct IlluminationZoneControlDataPiecewise<T>
				where T : unmanaged
			{
				public T ColorA;
				public T ColorB;
				public IlluminationZoneControlDataPiecewiseLinear Linear;
			}

			[StructLayout(LayoutKind.Explicit, Size = 128)]
			public struct IlluminationZoneControlDataRgb
			{
				[FieldOffset(0)]
				public IlluminationZoneControlDataManualRgb Manual;
				[FieldOffset(0)]
				public IlluminationZoneControlDataPiecewise<IlluminationZoneControlDataManualRgb> Piecewise;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved0;
				[FieldOffset(64)]
				private readonly ByteArray64 _reserved1;
			}

			[StructLayout(LayoutKind.Explicit, Size = 128)]
			public struct IlluminationZoneControlDataColorFixed
			{
				[FieldOffset(0)]
				public IlluminationZoneControlDataManualColorFixed Manual;
				[FieldOffset(0)]
				public IlluminationZoneControlDataPiecewise<IlluminationZoneControlDataManualColorFixed> Piecewise;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved0;
				[FieldOffset(64)]
				private readonly ByteArray64 _reserved1;
			}

			[StructLayout(LayoutKind.Explicit, Size = 128)]
			public struct IlluminationZoneControlDataRgbw
			{
				[FieldOffset(0)]
				public IlluminationZoneControlDataManualRgbw Manual;
				[FieldOffset(0)]
				public IlluminationZoneControlDataPiecewise<IlluminationZoneControlDataManualRgbw> Piecewise;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved0;
				[FieldOffset(64)]
				private readonly ByteArray64 _reserved1;
			}

			[StructLayout(LayoutKind.Explicit, Size = 128)]
			public struct IlluminationZoneControlDataSingleColor
			{
				[FieldOffset(0)]
				public IlluminationZoneControlDataManualSingleColor Manual;
				[FieldOffset(0)]
				public IlluminationZoneControlDataPiecewise<IlluminationZoneControlDataManualSingleColor> Piecewise;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved0;
				[FieldOffset(64)]
				private readonly ByteArray64 _reserved1;
			}

			[StructLayout(LayoutKind.Explicit, Size = 128)]
			public struct IlluminationZoneControlData
			{
				[FieldOffset(0)]
				public IlluminationZoneControlDataRgb Rgb;
				[FieldOffset(0)]
				public IlluminationZoneControlDataColorFixed ColorFixed;
				[FieldOffset(0)]
				public IlluminationZoneControlDataRgbw Rgbw;
				[FieldOffset(0)]
				public IlluminationZoneControlDataSingleColor SingleColor;
				[FieldOffset(0)]
				private readonly ByteArray64 _reserved;
			}

			public struct IlluminationZoneControl
			{
				public IlluminationZoneType Type;
				public IlluminationControlMode ControlMode;
				public IlluminationZoneControlData Data;
				private readonly ByteArray64 _reserved;
			}

			[InlineArray(32)]
			public struct IlluminationZoneControlArray
			{
				private IlluminationZoneControl _element0;
			}

			internal struct IlluminationZoneControlQuery
			{
				public uint Version;
				private uint _flags;
				public int ZoneCount;
				private readonly ByteArray64 _reserved;
				public IlluminationZoneControlArray Zones;

				public bool DefaultValues
				{
					get => (_flags & 0x1) != 0;
					set => _flags = value ? _flags | 1 : _flags & ~1U;
				}
			}
			internal struct CallbackSettings
			{
				public nint Parameter;
				private ByteArray64 _reserved;
			}

			internal struct PeriodicCallbackSettings
			{
				public CallbackSettings Common;
				public uint CallbackPeriodInMilliseconds;
				private ByteArray64 _reserved;
			}

			internal struct CallbackData
			{
				public nint Parameter;
				private ByteArray64 _reserved;
			}

			public enum UtilizationDomain
			{
				Graphics = 0,
				FrameBuffer = 1,
				Video = 2,
			}

			internal struct UtilizationData
			{
				public UtilizationDomain Domain;
				public uint UtilizationPercent;
				private ByteArray61 _reserved;
			}

			[InlineArray(4)]
			internal struct UtilizationDataArray
			{
				private UtilizationData _element0;
			}

			internal struct CallbackUtilizationData
			{
				public CallbackData Common;
				public uint UtilizationCount;
				public ulong Timestamp;
				private ByteArray64 _reserved;
				public UtilizationDataArray Utilizations;
			}

			internal unsafe struct UtilizationPeriodicCallbackSettings
			{
				public uint Version;
				public PeriodicCallbackSettings Settings;
				public delegate* unmanaged[Cdecl]<nint, CallbackUtilizationData*, void> Callback;
				private readonly ByteArray64 _reserved;
			}

			internal struct FanCoolerStatus
			{
				public uint FanId;
				public uint SpeedInRotationsPerMinute;
				public uint MinimumPower;
				public uint MaximumPower;
				public uint CurrentPower;
				public uint Unknown05;
				public uint Unknown06;
				public uint Unknown07;
				public uint Unknown08;
				public uint Unknown09;
				public uint Unknown10;
				public uint Unknown11;
				public uint Unknown12;
			}

			[InlineArray(32)]
			internal struct FanCoolerStatusArray
			{
				private FanCoolerStatus _element0;
			}

			internal struct FanCoolersStatus
			{
				public uint Version;
				public uint Count;

				public uint Unknown0;
				public uint Unknown1;
				public uint Unknown2;
				public uint Unknown3;
				public uint Unknown4;
				public uint Unknown5;
				public uint Unknown6;
				public uint Unknown7;

				public FanCoolerStatusArray FanCoolers;
			}

			internal struct FanCoolerControl
			{
				public uint FanId;
				public uint Power;
				public FanCoolingMode CoolingMode;
				public uint Unknown03;
				public uint Unknown04;
				public uint Unknown05;
				public uint Unknown06;
				public uint Unknown07;
				public uint Unknown08;
				public uint Unknown09;
				public uint Unknown10;
			}

			[InlineArray(32)]
			internal struct FanCoolerControlArray
			{
				private FanCoolerControl _element0;
			}

			internal struct FanCoolersControl
			{
				public uint Version;
				public uint Unknown0;
				public uint Count;

				public uint Unknown1;
				public uint Unknown2;
				public uint Unknown3;
				public uint Unknown4;
				public uint Unknown5;
				public uint Unknown6;
				public uint Unknown7;
				public uint Unknown8;

				public FanCoolerControlArray FanCoolers;
			}
		}
	}

	[DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	public static extern unsafe void* QueryInterface(uint functionId);

	public static unsafe string GetInterfaceVersionString()
	{
		ShortString str = default;
		ValidateResult(Functions.GetInterfaceVersionString(&str));
		return str.ToString();
	}

	private static void ValidateResult(uint status)
	{
		if (status != 0) ThrowExceptionForInvalidResult(status);
	}

	private static void ThrowExceptionForInvalidResult(uint status) => throw new NvApiException(status, GetErrorMessage(status));

	public static unsafe string? GetErrorMessage(uint status)
	{
		ShortString str = default;
		ValidateResult(Functions.GetErrorMessage(status, &str));
		return str.ToString();
	}

	public static unsafe PhysicalGpu[] GetPhysicalGpus()
	{
		nint* array = stackalloc nint[64];
		int count;
		ValidateResult(Functions.EnumPhysicalGPUs(array, &count));
		return new Span<PhysicalGpu>((PhysicalGpu*)array, count).ToArray();
	}

	public static class System
	{
		public static unsafe (uint DriverVersion, string BuildBranch) GetDriverAndBranchVersion()
		{
			uint driverVersion = 0;
			ShortString buildBranch = default;
			ValidateResult(Functions.System.GetDriverAndBranchVersion(&driverVersion, &buildBranch));
			return (driverVersion, buildBranch.ToString());
		}

		public static unsafe void GetGpuAndOutputIdFromDisplayId(uint displayId, out PhysicalGpu physicalGpu, out uint outputId)
		{
			nint gpu = 0;
			uint oid = 0;
			ValidateResult(Functions.System.GetGpuAndOutputIdFromDisplayId(displayId, &gpu, &oid));
			physicalGpu = Unsafe.As<nint, PhysicalGpu>(ref gpu);
			outputId = oid;
		}
	}

	public readonly struct PhysicalGpu : IEquatable<PhysicalGpu>
	{
		private readonly nint _handle;

		public bool IsValid => _handle != 0;

		public unsafe string GetFullName()
		{
			ShortString str = default;
			ValidateResult(Functions.Gpu.GetFullName(_handle, &str));
			return str.ToString();
		}

		public unsafe uint GetBusId()
		{
			uint busId;
			ValidateResult(Functions.Gpu.GetBusId(_handle, &busId));
			return busId;
		}

		public unsafe uint GetBusSlotId()
		{
			uint busSlotId;
			ValidateResult(Functions.Gpu.GetBusSlotId(_handle, &busSlotId));
			return busSlotId;
		}

		public unsafe byte[] GetBoardNumber()
		{
			var boardInfo = new Gpu.BoardInfo { Version = StructVersion<Gpu.BoardInfo>(1) };
			ValidateResult(Functions.Gpu.GetBoardInfo(_handle, &boardInfo));
			return boardInfo.BoardNumber.ToByteArray();
		}

		private static byte[] ReadSerialNumber(ReadOnlySpan<byte> data)
		{
			int endIndex = data.IndexOf((byte)0);

			if (endIndex < 0) return Array.Empty<byte>();

			return data[..endIndex].ToArray();
		}

		public unsafe byte[] GetSerialNumber()
		{
			var data = new ByteArray64();
			ValidateResult(Functions.Gpu.GetSerialNumber(_handle, &data));
			return ReadSerialNumber(data);
		}

		public unsafe Gpu.DisplayIdInfo[] GetConnectedDisplays(Gpu.ConnectedIdFlags flags)
		{
			uint version = StructVersion<Gpu.DisplayIdInfo>(3);
			uint count = 0;
			ValidateResult(Functions.Gpu.GetConnectedDisplayIds(_handle, null, &count, flags));
			while (true)
			{
				var displays = new Gpu.DisplayIdInfo[count];
				for (int i = 0; i < displays.Length; i++)
				{
					displays[i].Version = version;
				}
				fixed (Gpu.DisplayIdInfo* displayPointer = displays)
				{
					ValidateResult(Functions.Gpu.GetConnectedDisplayIds(_handle, displayPointer, &count, flags));
				}
				if (count <= displays.Length)
				{
					if (count < displays.Length)
					{
						displays = displays[..(int)count];
					}
					return displays;
				}
			}
		}

		// Actually assuming that I2C speed in KHz this is what the API returns, because I have limited information and that's what looks like the most realistic.
		public unsafe uint[] GetI2cPortSpeeds()
		{
			if (!HasI2c) return [];

			var infos = stackalloc Gpu.I2cPortInfo[16];
			infos[0].Version = StructVersion<Gpu.I2cPortInfo>(1);

			ValidateResult(Functions.Gpu.GetI2cPortsInfo(_handle, infos, 16));

			var speeds = stackalloc uint[16];
			int i;
			for (i = 0; i < 16; i++)
			{
				if (infos[i].Index != i)
				{
					if (infos[i].Index != 0) throw new InvalidOperationException("TODO: The indices returned are not contiguous.");
					break;
				}
				speeds[i] = infos[i].Data;
			}
			return new ReadOnlySpan<uint>(speeds, i).ToArray();
		}

		public unsafe byte[] GetEdid(uint outputId)
		{
			var edid = new Edid() { Version = StructVersion<Edid>(3) };
			ValidateResult(Functions.Gpu.GetEdid(_handle, outputId, &edid));
			var data = new byte[edid.EdidSize];
			((ReadOnlySpan<byte>)edid.Data)[..data.Length].CopyTo(data);
			if (edid.EdidSize > 256)
			{
				uint blockCount = (edid.EdidSize + 255) / 256;
				for (uint i = 1; i < blockCount; i++)
				{
					do
					{
						edid.EdidId = i;
						edid.Offset = blockCount * 256;
						ValidateResult(Functions.Gpu.GetEdid(_handle, outputId, &edid));
					}
					while (edid.EdidId == i);
					((ReadOnlySpan<byte>)edid.Data).Slice((int)edid.Offset, Math.Min(256, data.Length - (int)edid.Offset)).CopyTo(data);
				}
			}
			return data;
		}

		// It seems there are few flags:
		// Bit 0: Can be on or off, but is generally sent as 1 by custom RGB code found on internet
		// Bits 1-3: Exclusive. Can be all 0 but at most one can be set. Seem to somewhat alter the behavior, but in ways that I don't understand.
		// The value 1 seems relatively safe, and I haven't observed any obvious change in behavior by using it.
		private const ulong I2cUnknownFlags = 0x0000000000000000;

		public unsafe void I2CMonitorWrite(uint outputId, byte address, byte register, ReadOnlyMemory<byte> data)
		{
			if (!HasI2c) throw new NotSupportedException();

			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = false,
				DeviceAddress = (byte)(address & 0xFE),
				RegisterAddress = &register,
				RegisterAddressSize = 1,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ulong unknown = I2cUnknownFlags;
			ValidateResult(Functions.I2CWriteEx(_handle, &info, (uint*)&unknown));
		}

		public unsafe void I2CMonitorWrite(uint outputId, byte address, ReadOnlyMemory<byte> data)
		{
			if (!HasI2c) throw new NotSupportedException();

			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = false,
				DeviceAddress = (byte)(address & 0xFE),
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ulong unknown = I2cUnknownFlags;
			ValidateResult(Functions.I2CWriteEx(_handle, &info, (uint*)&unknown));
		}

		public unsafe void I2CMonitorRead(uint outputId, byte address, byte register, ReadOnlyMemory<byte> data)
		{
			if (!HasI2c) throw new NotSupportedException();

			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = false,
				DeviceAddress = (byte)(address & 0xFE),
				RegisterAddress = &register,
				RegisterAddressSize = 1,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ulong unknown = I2cUnknownFlags;
			ValidateResult(Functions.I2CReadEx(_handle, &info, (uint*)&unknown));
		}

		public unsafe void I2CMonitorRead(uint outputId, byte address, ReadOnlyMemory<byte> data)
		{
			if (!HasI2c) throw new NotSupportedException();

			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = false,
				DeviceAddress = address,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ulong unknown = I2cUnknownFlags;
			ValidateResult(Functions.I2CReadEx(_handle, &info, (uint*)&unknown));
		}

		public unsafe bool SupportsIllumination(Gpu.IlluminationZone zone)
		{
			if (!HasIllumination) return false;
			var query = new Gpu.IlluminationQuery { Version = StructVersion<Gpu.IlluminationQuery>(1), PhysicalGpuHandle = _handle, Attribute = (Gpu.IlluminationAttribute)zone };
			ValidateResult(Functions.Gpu.QueryIlluminationSupport(&query));
			return query.Value != 0;
		}

		public unsafe Gpu.Client.IlluminationDeviceInfo[] GetIlluminationDevices()
		{
			if (!HasIllumination) return [];
			var query = new Gpu.Client.IlluminationDeviceInfoQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceInfoQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumDevicesGetInfo(_handle, &query));
			return ((Span<Gpu.Client.IlluminationDeviceInfo>)query.Devices)[..query.DeviceCount].ToArray();
		}

		public unsafe Gpu.Client.IlluminationDeviceControl[] GetIlluminationDeviceControls()
		{
			if (!HasIllumination) return [];
			var query = new Gpu.Client.IlluminationDeviceControlQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceControlQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumDevicesGetControl(_handle, &query));
			return ((Span<Gpu.Client.IlluminationDeviceControl>)query.Devices)[..query.DeviceCount].ToArray();
		}

		public unsafe void SetIlluminationDeviceControls(Gpu.Client.IlluminationDeviceControl[] controls)
		{
			ArgumentNullException.ThrowIfNull(controls);
			if (!HasIllumination) throw new NotSupportedException();
			if (controls.Length > 32) throw new ArgumentException();
			var query = new Gpu.Client.IlluminationDeviceControlQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceControlQuery>(1), DeviceCount = controls.Length };
			controls.AsSpan().CopyTo(query.Devices);
			ValidateResult(Functions.Gpu.ClientIllumDevicesSetControl(_handle, &query));
		}

		public unsafe Gpu.Client.IlluminationZoneInfo[] GetIlluminationZones()
		{
			if (!HasIllumination) return [];
			var query = new Gpu.Client.IlluminationZoneInfoQuery { Version = StructVersion<Gpu.Client.IlluminationZoneInfoQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumZonesGetInfo(_handle, &query));
			return ((Span<Gpu.Client.IlluminationZoneInfo>)query.Zones)[..query.ZoneCount].ToArray();
		}

		public unsafe Gpu.Client.IlluminationZoneControl[] GetIlluminationZoneControls(bool shouldReturnPersisted)
		{
			if (!HasIllumination) return [];
			var query = new Gpu.Client.IlluminationZoneControlQuery { Version = StructVersion<Gpu.Client.IlluminationZoneControlQuery>(1), DefaultValues = shouldReturnPersisted };
			ValidateResult(Functions.Gpu.ClientIllumZonesGetControl(_handle, &query));
			return ((Span<Gpu.Client.IlluminationZoneControl>)query.Zones)[..query.ZoneCount].ToArray();
		}

		public unsafe void SetIlluminationZoneControls(Gpu.Client.IlluminationZoneControl[] controls, bool shouldPersist)
		{
			ArgumentNullException.ThrowIfNull(controls);
			if (!HasIllumination) throw new NotSupportedException();
			if (controls.Length > 32) throw new ArgumentException();
			var query = new Gpu.Client.IlluminationZoneControlQuery { Version = StructVersion<Gpu.Client.IlluminationZoneControlQuery>(1), DefaultValues = shouldPersist };
			controls.AsSpan().CopyTo(query.Zones);
			ValidateResult(Functions.Gpu.ClientIllumZonesSetControl(_handle, &query));
		}

		public unsafe Gpu.ThermalSensor GetThermalSettings(uint sensorIndex)
		{
			if (!HasThermals) throw new NotSupportedException();
			var thermalSettings = new Gpu.ThermalSettings { Version = StructVersion<Gpu.ThermalSettings>(Gpu.ThermalSettingsVersion) };
			ValidateResult(Functions.Gpu.GetThermalSettings(_handle, sensorIndex, &thermalSettings));
			if (thermalSettings.Count != 1) throw new InvalidOperationException("Invalid thermal reading count.");
			return thermalSettings.Sensors[0];
		}

		public unsafe int GetThermalSettings(Span<Gpu.ThermalSensor> thermalSensors)
		{
			if (!HasThermals) return 0;
			var thermalSettings = new Gpu.ThermalSettings { Version = StructVersion<Gpu.ThermalSettings>(Gpu.ThermalSettingsVersion) };
			ValidateResult(Functions.Gpu.GetThermalSettings(_handle, 15, &thermalSettings));
			if (thermalSettings.Count > 3) throw new InvalidOperationException("Invalid thermal reading count.");
			((ReadOnlySpan<Gpu.ThermalSensor>)thermalSettings.Sensors)[..(int)thermalSettings.Count].CopyTo(thermalSensors);
			return (int)thermalSettings.Count;
		}

		public unsafe Gpu.CoolerInformation GetCoolerSettings(Gpu.CoolerTarget coolerTarget)
		{
			if (Functions.Gpu.GetCoolerSettings == null) throw new NotSupportedException();
			var coolerSettings = new Gpu.CoolerSettings { Version = StructVersion<Gpu.CoolerSettings>(Gpu.CoolerSettingsVersion) };
			ValidateResult(Functions.Gpu.GetCoolerSettings(_handle, (uint)coolerTarget, &coolerSettings));
			if (coolerSettings.Count != 1) throw new InvalidOperationException("Invalid cooler count.");
			return coolerSettings.Coolers[0];
		}

		public unsafe int GetCoolerSettings(Span<Gpu.CoolerInformation> coolers)
		{
			if (Functions.Gpu.GetCoolerSettings == null) throw new NotSupportedException();
			var coolerSettings = new Gpu.CoolerSettings { Version = StructVersion<Gpu.CoolerSettings>(Gpu.CoolerSettingsVersion) };
			ValidateResult(Functions.Gpu.GetCoolerSettings(_handle, (uint)Gpu.CoolerTarget.All, &coolerSettings));
			if (coolerSettings.Count > 3) throw new InvalidOperationException("Invalid cooler count.");
			((ReadOnlySpan<Gpu.CoolerInformation>)coolerSettings.Coolers)[..(int)coolerSettings.Count].CopyTo(coolers);
			return (int)coolerSettings.Count;
		}

		public unsafe int GetClockFrequencies(Gpu.ClockType clockType, Span<GpuClockFrequency> clockFrequencies)
		{
			if (!HasClockFrequencies) return 0;
			var apiClockFrequencies = new Gpu.ClockFrequencies { Version = StructVersion<Gpu.ClockFrequencies>(3), ClockType = clockType };
			ValidateResult(Functions.Gpu.GetAllClockFrequencies(_handle, &apiClockFrequencies));
			int count = 0;
			var domains = (ReadOnlySpan<Gpu.ClockFrequency>)apiClockFrequencies.Domains;
			for (int i = 0; i < domains.Length; i++)
			{
				if (domains[i].IsPresent)
				{
					clockFrequencies[count++] = new GpuClockFrequency((Gpu.PublicClock)i, domains[i].FrequencyInKiloHertz);
				}
			}
			return count;
		}

		public unsafe uint GetTachReading()
		{
			if (Functions.Gpu.GetTachReading == null) throw new NotSupportedException();
			uint reading;
			ValidateResult(Functions.Gpu.GetTachReading(_handle, &reading));
			return reading;
		}

		public unsafe int GetFanCoolersStatus(Span<GpuFanStatus> fanStatuses)
		{
			if (Functions.Gpu.ClientFanCoolersGetStatus == null) return 0;
			var status = new Gpu.Client.FanCoolersStatus { Version = StructVersion<Gpu.Client.FanCoolersStatus>(1) };
			ValidateResult(Functions.Gpu.ClientFanCoolersGetStatus(_handle, &status));
			if (status.Count > 32) throw new InvalidOperationException("Invalid fan cooler count.");
			if (fanStatuses.Length < status.Count) throw new ArgumentException("Provided storage is not large enough.");
			var items = ((ReadOnlySpan<Gpu.Client.FanCoolerStatus>)status.FanCoolers)[..(int)status.Count];
			for (int i = 0; i < status.Count; i++)
			{
				ref readonly var item = ref items[i];
				fanStatuses[i] = new GpuFanStatus(item.FanId, item.SpeedInRotationsPerMinute, item.MinimumPower, item.MaximumPower, item.CurrentPower);
			}
			return (int)status.Count;
		}

		public unsafe int GetFanCoolersControl(Span<GpuFanControl> fanControls)
		{
			if (Functions.Gpu.ClientFanCoolersGetControl == null) return 0;
			var status = new Gpu.Client.FanCoolersControl { Version = StructVersion<Gpu.Client.FanCoolersControl>(1) };
			ValidateResult(Functions.Gpu.ClientFanCoolersGetControl(_handle, &status));
			if (status.Count > 32) throw new InvalidOperationException("Invalid fan cooler count.");
			if (fanControls.Length < status.Count) throw new ArgumentException("Provided storage is not large enough.");
			var items = ((ReadOnlySpan<Gpu.Client.FanCoolerControl>)status.FanCoolers)[..(int)status.Count];
			for (int i = 0; i < status.Count; i++)
			{
				ref readonly var item = ref items[i];
				fanControls[i] = new(item.FanId, item.Power, item.CoolingMode);
			}
			return (int)status.Count;
		}

		public unsafe void SetFanCoolersControl(ReadOnlySpan<GpuFanControl> fanControls)
		{
			if (Functions.Gpu.ClientFanCoolersSetControl == null) return;
			var status = new Gpu.Client.FanCoolersControl { Version = StructVersion<Gpu.Client.FanCoolersControl>(1) };
			if ((uint)(fanControls.Length - 1) > 32) throw new ArgumentException("Invalid fan cooler count.");
			var items = ((Span<Gpu.Client.FanCoolerControl>)status.FanCoolers)[..fanControls.Length];
			for (int i = 0; i < fanControls.Length; i++)
			{
				ref readonly var fanControl = ref fanControls[i];
				ref var item = ref items[i];
				item.FanId = fanControl.FanId;
				item.Power = fanControl.Power;
				item.CoolingMode = fanControl.CoolingMode;
			}
			ValidateResult(Functions.Gpu.ClientFanCoolersSetControl(_handle, &status));
		}

		[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
		private static unsafe void OnUtilizationUpdate(nint physicalGpuHandle, Gpu.Client.CallbackUtilizationData* data)
		{
			try
			{
				var gcHandle = GCHandle.FromIntPtr(data->Common.Parameter);
				if (gcHandle.Target is ChannelWriter<GpuClientUtilizationData> writer)
				{
					var dateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)(data->Timestamp / 1000)).UtcDateTime;
					foreach (ref var utilization in ((Span<Gpu.Client.UtilizationData>)data->Utilizations)[..(int)data->UtilizationCount])
					{
						writer.TryWrite(new GpuClientUtilizationData(dateTime, utilization.UtilizationPercent, utilization.Domain));
					}
				}
			}
			catch
			{
			}
		}

		public IAsyncEnumerable<GpuClientUtilizationData> WatchUtilizationAsync(uint period, CancellationToken cancellationToken)
			=> WatchUtilizationAsync(_handle, period, cancellationToken);

		private static async IAsyncEnumerable<GpuClientUtilizationData> WatchUtilizationAsync(nint handle, uint period, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			if (!HasUtilizationSamples) throw new NotSupportedException();

			var channel = Channel.CreateBounded<GpuClientUtilizationData>(SharedOptions.ChannelOptions);
			var gcHandle = GCHandle.Alloc(channel.Writer);
			try
			{
				RegisterForUtilizationSampleUpdates(handle, period, GCHandle.ToIntPtr(gcHandle));
				try
				{
					await foreach (var utilization in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
					{
						yield return utilization;
					}
				}
				finally
				{
					ClientUnregisterForUtilizationSampleUpdates(handle);
				}
			}
			finally
			{
				gcHandle.Free();
			}
		}

		private static unsafe void ClientUnregisterForUtilizationSampleUpdates(nint handle)
		{
			var settings = new Gpu.Client.UtilizationPeriodicCallbackSettings { Version = StructVersion<Gpu.Client.UtilizationPeriodicCallbackSettings>(1) };
			ValidateResult(Functions.Gpu.ClientRegisterForUtilizationSampleUpdates(handle, &settings));
		}

		private static unsafe void RegisterForUtilizationSampleUpdates(nint handle, uint period, nint parameter)
		{
			var settings = new Gpu.Client.UtilizationPeriodicCallbackSettings
			{
				Version = StructVersion<Gpu.Client.UtilizationPeriodicCallbackSettings>(1),
				Settings =
				{
					Common = { Parameter = parameter },
					CallbackPeriodInMilliseconds = period,
				},
				Callback = &OnUtilizationUpdate,
			};
			ValidateResult(Functions.Gpu.ClientRegisterForUtilizationSampleUpdates(handle, &settings));
		}

		public override bool Equals(object? obj) => obj is PhysicalGpu gpu && Equals(gpu);
		public bool Equals(PhysicalGpu other) => _handle.Equals(other._handle);
		public override int GetHashCode() => HashCode.Combine(_handle);

		public static bool operator ==(PhysicalGpu left, PhysicalGpu right) => left.Equals(right);
		public static bool operator !=(PhysicalGpu left, PhysicalGpu right) => !(left == right);
	}

	public readonly struct GpuClientUtilizationData
	{
		public GpuClientUtilizationData(DateTime dateTime, uint perTenThousandValue, Gpu.Client.UtilizationDomain domain)
		{
			DateTime = dateTime;
			PerTenThousandValue = perTenThousandValue;
			Domain = domain;
		}

		public DateTime DateTime { get; }
		public uint PerTenThousandValue { get; }
		public Gpu.Client.UtilizationDomain Domain { get; }
	}

	public readonly struct GpuClockFrequency
	{
		public GpuClockFrequency(Gpu.PublicClock clock, uint frequencyInKiloHertz)
		{
			Clock = clock;
			FrequencyInKiloHertz = frequencyInKiloHertz;
		}

		public Gpu.PublicClock Clock { get; }
		public uint FrequencyInKiloHertz { get; }
	}

	public readonly struct GpuFanStatus(uint fanId, uint speedInRotationsPerMinute, uint minimumPower, uint maximumPower, uint currentPower)
	{
		// Unsure whether this is an index or an enumeration like ThermalTarget, etc. Must be conservative when using that value.
		public uint FanId { get; } = fanId;
		public uint SpeedInRotationsPerMinute { get; } = speedInRotationsPerMinute;
		public uint MinimumPower { get; } = minimumPower;
		public uint MaximumPower { get; } = maximumPower;
		public uint CurrentPower { get; } = currentPower;
	}

	public enum FanCoolingMode : uint
	{
		Automatic = 0,
		Manual = 1,
	}

	public readonly struct GpuFanControl(uint fanId, uint power, FanCoolingMode coolingMode)
	{
		public uint FanId { get; } = fanId;
		public uint Power { get; } = power;
		public FanCoolingMode CoolingMode { get; } = coolingMode;
	}
}

#pragma warning restore CS0169
#pragma warning restore CS0649
#pragma warning restore IDE0044 // Add readonly modifier
