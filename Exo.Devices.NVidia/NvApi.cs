using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Exo.Devices.NVidia;

#pragma warning disable IDE0044 // Add readonly modifier

internal unsafe sealed class NvApi
{
	private static readonly NvApi Instance = new();

	static NvApi() { }

	private NvApi()
	{
		Functions.Initialize();
	}

	~NvApi()
	{
		Functions.Unload();
	}

	// Define and load all the function pointers that we are gonna need.
	private static class Functions
	{
		public static readonly delegate* unmanaged[Cdecl]<uint> Initialize = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x0150e828);
		public static readonly delegate* unmanaged[Cdecl]<uint> Unload = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xd22bdd7e);
		public static readonly delegate* unmanaged[Cdecl]<uint, ShortString*, uint> GetErrorMessage = (delegate* unmanaged[Cdecl]<uint, ShortString*, uint>)QueryInterface(0x6c2d048c);
		public static readonly delegate* unmanaged[Cdecl]<ShortString*, uint> GetInterfaceVersionString = (delegate* unmanaged[Cdecl]<ShortString*, uint>)QueryInterface(0x01053fa5);
		public static readonly delegate* unmanaged[Cdecl]<nint*, int*, uint> EnumPhysicalGPUs = (delegate* unmanaged[Cdecl]<nint*, int*, uint>)QueryInterface(0xe5ac921f);
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint> I2CRead = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint>)QueryInterface(0x2fde12c5);
		public static readonly delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint> I2CWrite = (delegate* unmanaged[Cdecl]<nint, I2CInfo*, uint>)QueryInterface(0xe812eb07);

		public static class Gpu
		{
			public static readonly delegate* unmanaged[Cdecl]<nint, uint, Edid*, uint> GetEdid = (delegate* unmanaged[Cdecl]<nint, uint, Edid*, uint>)QueryInterface(0x37d32e69);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.BoardInfo*, uint> GetBoardInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.BoardInfo*, uint>)QueryInterface(0x22d54523);
			public static readonly delegate* unmanaged[Cdecl]<nint, ShortString*, uint> GetFullName = (delegate* unmanaged[Cdecl]<nint, ShortString*, uint>)QueryInterface(0xceee8e9f);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x1be0b8e5);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusSlotId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x2a0a350f);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.DisplayIdInfo*, uint*, NvApi.Gpu.ConnectedIdFlags, uint> GetConnectedDisplayIds = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.DisplayIdInfo*, uint*, NvApi.Gpu.ConnectedIdFlags, uint>)QueryInterface(0x0078dba2);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> QueryIlluminationSupport = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0xa629da31);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> GetIllumination = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0x9a1b9365);
			public static readonly delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint> SetIllumination = (delegate* unmanaged[Cdecl]<NvApi.Gpu.IlluminationQuery*, uint>)QueryInterface(0x0254a187);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceInfoQuery*, uint> ClientIllumDevicesGetInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceInfoQuery*, uint>)QueryInterface(0xd4100e58);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint> ClientIllumDevicesGetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint>)QueryInterface(0x73c01d58);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint> ClientIllumDevicesSetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationDeviceControlQuery*, uint>)QueryInterface(0x57024c62);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneInfoQuery*, uint> ClientIllumZonesGetInfo = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneInfoQuery*, uint>)QueryInterface(0x4b81241b);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint> ClientIllumZonesGetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint>)QueryInterface(0x3dbf5764);
			public static readonly delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint> ClientIllumZonesSetControl = (delegate* unmanaged[Cdecl]<nint, NvApi.Gpu.Client.IlluminationZoneControlQuery*, uint>)QueryInterface(0x197d065e);
		}

		public static class System
		{
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

	private static uint StructVersion<T>(int version)
		where T : unmanaged
		=> (uint)(sizeof(T) | version << 16);

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
		}
	}

	[DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
	public static extern void* QueryInterface(uint functionId);

	public static string GetInterfaceVersionString()
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

	public static string? GetErrorMessage(uint status)
	{
		ShortString str = default;
		ValidateResult(Functions.GetErrorMessage(status, &str));
		return str.ToString();
	}

	public static PhysicalGpu[] GetPhysicalGpus()
	{
		nint* array = stackalloc nint[64];
		int count;
		ValidateResult(Functions.EnumPhysicalGPUs(array, &count));
		return new Span<PhysicalGpu>((PhysicalGpu*)array, count).ToArray();
	}

	public static class System
	{
		public static void GetGpuAndOutputIdFromDisplayId(uint displayId, out PhysicalGpu physicalGpu, out uint outputId)
		{
			nint gpu = 0;
			uint oid = 0;
			ValidateResult(Functions.System.GetGpuAndOutputIdFromDisplayId(displayId, &gpu, &oid));
			physicalGpu = Unsafe.As<nint, PhysicalGpu>(ref gpu);
			outputId = oid;
		}
	}

	public readonly struct PhysicalGpu
	{
		private readonly nint _handle;

		public bool IsValid => _handle != 0;

		public string GetFullName()
		{
			ShortString str = default;
			ValidateResult(Functions.Gpu.GetFullName(_handle, &str));
			return str.ToString();
		}

		public uint GetBusId()
		{
			uint busId;
			ValidateResult(Functions.Gpu.GetBusId(_handle, &busId));
			return busId;
		}

		public uint GetBusSlotId()
		{
			uint busSlotId;
			ValidateResult(Functions.Gpu.GetBusSlotId(_handle, &busSlotId));
			return busSlotId;
		}

		public byte[] GetBoardNumber()
		{
			var boardInfo = new Gpu.BoardInfo { Version = StructVersion<Gpu.BoardInfo>(1) };
			ValidateResult(Functions.Gpu.GetBoardInfo(_handle, &boardInfo));
			return boardInfo.BoardNumber.ToByteArray();
		}

		public Gpu.DisplayIdInfo[] GetConnectedDisplays(Gpu.ConnectedIdFlags flags)
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

		public byte[] GetEdid(uint outputId)
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

		public void I2CMonitorWrite(uint outputId, byte address, byte register, ReadOnlyMemory<byte> data)
		{
			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = true,
				DeviceAddress = address,
				RegisterAddress = &register,
				RegisterAddressSize = 1,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ValidateResult(Functions.I2CWrite(_handle, &info));
		}

		public void I2CMonitorRead(uint outputId, byte address, byte register, ReadOnlyMemory<byte> data)
		{
			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = true,
				DeviceAddress = address,
				RegisterAddress = &register,
				RegisterAddressSize = 1,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ValidateResult(Functions.I2CRead(_handle, &info));
		}

		public void I2CMonitorRead(uint outputId, byte address, ReadOnlyMemory<byte> data)
		{
			using var handle = data.Pin();
			var info = new I2CInfo
			{
				Version = StructVersion<I2CInfo>(3),
				DisplayMask = outputId,
				IsDdcPort = true,
				DeviceAddress = address,
				Data = (byte*)handle.Pointer,
				Size = (uint)data.Length,
				DeprecatedSpeed = 0xFFFF,
			};
			ValidateResult(Functions.I2CRead(_handle, &info));
		}

		public bool SupportsIllumination(Gpu.IlluminationZone zone)
		{
			var query = new Gpu.IlluminationQuery { Version = StructVersion<Gpu.IlluminationQuery>(1), PhysicalGpuHandle = _handle, Attribute = (Gpu.IlluminationAttribute)zone };
			ValidateResult(Functions.Gpu.QueryIlluminationSupport(&query));
			return query.Value != 0;
		}

		public Gpu.Client.IlluminationDeviceInfo[] GetIlluminationDevices()
		{
			var query = new Gpu.Client.IlluminationDeviceInfoQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceInfoQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumDevicesGetInfo(_handle, &query));
			return ((Span<Gpu.Client.IlluminationDeviceInfo>)query.Devices)[..query.DeviceCount].ToArray();
		}

		public Gpu.Client.IlluminationDeviceControl[] GetIlluminationDeviceControls()
		{
			var query = new Gpu.Client.IlluminationDeviceControlQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceControlQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumDevicesGetControl(_handle, &query));
			return ((Span<Gpu.Client.IlluminationDeviceControl>)query.Devices)[..query.DeviceCount].ToArray();
		}

		public void SetIlluminationDeviceControls(Gpu.Client.IlluminationDeviceControl[] controls)
		{
			ArgumentNullException.ThrowIfNull(controls);
			if (controls.Length > 32) throw new ArgumentException();
			var query = new Gpu.Client.IlluminationDeviceControlQuery { Version = StructVersion<Gpu.Client.IlluminationDeviceControlQuery>(1), DeviceCount = controls.Length };
			controls.AsSpan().CopyTo(query.Devices);
			ValidateResult(Functions.Gpu.ClientIllumDevicesSetControl(_handle, &query));
		}

		public Gpu.Client.IlluminationZoneInfo[] GetIlluminationZones()
		{
			var query = new Gpu.Client.IlluminationZoneInfoQuery { Version = StructVersion<Gpu.Client.IlluminationZoneInfoQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumZonesGetInfo(_handle, &query));
			return ((Span<Gpu.Client.IlluminationZoneInfo>)query.Zones)[..query.ZoneCount].ToArray();
		}

		public Gpu.Client.IlluminationZoneControl[] GetIlluminationZoneControls(bool shouldReturnPersisted)
		{
			var query = new Gpu.Client.IlluminationZoneControlQuery { Version = StructVersion<Gpu.Client.IlluminationZoneControlQuery>(1), DefaultValues = shouldReturnPersisted };
			ValidateResult(Functions.Gpu.ClientIllumZonesGetControl(_handle, &query));
			return ((Span<Gpu.Client.IlluminationZoneControl>)query.Zones)[..query.ZoneCount].ToArray();
		}

		public void SetIlluminationZoneControls(Gpu.Client.IlluminationZoneControl[] controls, bool shouldPersist)
		{
			ArgumentNullException.ThrowIfNull(controls);
			if (controls.Length > 32) throw new ArgumentException();
			var query = new Gpu.Client.IlluminationZoneControlQuery { Version = StructVersion<Gpu.Client.IlluminationZoneControlQuery>(1), DefaultValues = shouldPersist };
			controls.AsSpan().CopyTo(query.Zones);
			ValidateResult(Functions.Gpu.ClientIllumZonesSetControl(_handle, &query));
		}
	}
}
