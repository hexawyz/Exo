using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
		public static readonly delegate* unmanaged[Cdecl]<nint*, void*, uint> I2CRead = (delegate* unmanaged[Cdecl]<nint*, void*, uint>)QueryInterface(0x2fde12c5);
		public static readonly delegate* unmanaged[Cdecl]<nint*, void*, uint> I2CWrite = (delegate* unmanaged[Cdecl]<nint*, void*, uint>)QueryInterface(0xe812eb07);

		public static class Gpu
		{
			public static readonly delegate* unmanaged[Cdecl]<nint, ShortString*, uint> GetFullName = (delegate* unmanaged[Cdecl]<nint, ShortString*, uint>)QueryInterface(0xceee8e9f);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x1be0b8e5);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusSlotId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x2a0a350f);
			public static readonly delegate* unmanaged[Cdecl]<IlluminationQuery*, uint> QueryIlluminationSupport = (delegate* unmanaged[Cdecl]<IlluminationQuery*, uint>)QueryInterface(0xa629da31);
			public static readonly delegate* unmanaged[Cdecl]<IlluminationQuery*, uint> GetIllumination = (delegate* unmanaged[Cdecl]<IlluminationQuery*, uint>)QueryInterface(0x9a1b9365);
			public static readonly delegate* unmanaged[Cdecl]<IlluminationQuery*, uint> SetIllumination = (delegate* unmanaged[Cdecl]<IlluminationQuery*, uint>)QueryInterface(0x0254a187);
			public static readonly delegate* unmanaged[Cdecl]<nint, GpuClientIlluminationDeviceInfoQuery*, uint> ClientIllumDevicesGetInfo = (delegate* unmanaged[Cdecl]<nint, GpuClientIlluminationDeviceInfoQuery*, uint>)QueryInterface(0xd4100e58);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint> ClientIllumDevicesGetControl = (delegate* unmanaged[Cdecl]<nint, uint>)QueryInterface(0x73c01d58);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint> ClientIllumDevicesSetControl = (delegate* unmanaged[Cdecl]<nint, uint>)QueryInterface(0x57024c62);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint> ClientIllumZonesGetInfo = (delegate* unmanaged[Cdecl]<nint, uint>)QueryInterface(0x4b81241b);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint> ClientIllumZonesGetControl = (delegate* unmanaged[Cdecl]<nint, uint>)QueryInterface(0x3dbf5764);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint> ClientIllumZonesSetControl = (delegate* unmanaged[Cdecl]<nint, uint>)QueryInterface(0x197d065e);
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

	public enum GpuIlluminationZone : int
	{
		Logo,
		Sli,
	}

	private enum GpuIlluminationAttribute : int
	{
		LogoBrightness,
		SliBrightness,
	}

	[InlineArray(64)]
	private struct ByteArray64
	{
		private byte _element0;
	}

	// This fuses the Query, Get and Set structures, as they currently use the same API.
	// Query returns a boolean, and Get/Set expose the brightness.
	private struct IlluminationQuery
	{
		public uint Version;
		public nint PhysicalGpuHandle;
		public GpuIlluminationAttribute Attribute;
		public uint Value;
	}

	public enum GpuClientIlluminationDeviceType : int
	{
		Invalid = 0,
		McuV10 = 1,
		GpioPwmRgbw = 2,
		GpioPwmSingleColor = 3,
	}

	public struct GpuClientIlluminationDeviceInfoDataMcuV10
	{
		public byte I2CDeviceIndex;
	}

	public struct GpuClientIlluminationDeviceInfoDataGpioPwmRgbw
	{
		public byte GpioPinRed;
		public byte GpioPinGreen;
		public byte GpioPinBlue;
		public byte GpioPinWhite;
	}

	public struct GpuClientIlluminationDeviceInfoDataGpioPwmSingleColor
	{
		public byte GpioPinSingleColor;
	}

	[StructLayout(LayoutKind.Explicit)]
	public struct GpuClientIlluminationDeviceInfoData
	{
		[FieldOffset(0)]
		public GpuClientIlluminationDeviceInfoDataMcuV10 McuV10;
		[FieldOffset(0)]
		public GpuClientIlluminationDeviceInfoDataGpioPwmRgbw GpioPwmRgbwv10;
		[FieldOffset(0)]
		public GpuClientIlluminationDeviceInfoDataGpioPwmSingleColor GpioPwmSingleColorv10;
		[FieldOffset(0)]
		private ByteArray64 _reserved;
	}

	public struct GpuClientIlluminationDeviceInfo
	{
		public GpuClientIlluminationDeviceType DeviceType;
		public uint SupportedControlModes;
		public GpuClientIlluminationDeviceInfoData Data;
		private ByteArray64 _reserved;
	}

	[InlineArray(32)]
	private struct GpuClientIlluminationDeviceInfoArray
	{
		private GpuClientIlluminationDeviceInfo _element0;
	}

	private struct GpuClientIlluminationDeviceInfoQuery
	{
		public uint Version;
		public int DeviceCount;
		private readonly ByteArray64 _reserved;
		public GpuClientIlluminationDeviceInfoArray Devices;
	}

	[DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
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

	public static unsafe PhysicalGpu[] GetPhysicalGpus()
	{
		nint* array = stackalloc nint[64];
		int count;
		ValidateResult(Functions.EnumPhysicalGPUs(array, &count));
		return new Span<PhysicalGpu>((PhysicalGpu*)array, count).ToArray();
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

		public bool SupportsIllumination(GpuIlluminationZone zone)
		{
			var query = new IlluminationQuery { Version = StructVersion<IlluminationQuery>(1), PhysicalGpuHandle = _handle, Attribute = (GpuIlluminationAttribute)zone };
			ValidateResult(Functions.Gpu.QueryIlluminationSupport(&query));
			return query.Value != 0;
		}

		public GpuClientIlluminationDeviceInfo[] GetIlluminationDevices()
		{
			var query = new GpuClientIlluminationDeviceInfoQuery { Version = StructVersion<GpuClientIlluminationDeviceInfoQuery>(1) };
			ValidateResult(Functions.Gpu.ClientIllumDevicesGetInfo(_handle , & query));
			return ((Span<GpuClientIlluminationDeviceInfo>)query.Devices)[..query.DeviceCount].ToArray();
		}
	}
}
