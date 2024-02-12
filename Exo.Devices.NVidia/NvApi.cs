using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Devices.NVidia;

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

	internal struct ShortString
	{
#pragma warning disable IDE0044 // Add readonly modifier
		private long _0;
		private long _1;
		private long _2;
		private long _3;
		private long _4;
		private long _5;
		private long _6;
		private long _7;
#pragma warning restore IDE0044 // Add readonly modifier

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

	private static class Functions
	{
		public static readonly delegate* unmanaged[Cdecl]<uint> Initialize = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x0150e828);
		public static readonly delegate* unmanaged[Cdecl]<uint> Unload = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xd22bdd7e);
		public static readonly delegate* unmanaged[Cdecl]<uint, ShortString*,uint> GetErrorMessage = (delegate* unmanaged[Cdecl]<uint, ShortString*,uint>)QueryInterface(0x6c2d048c);
		public static readonly delegate* unmanaged[Cdecl]<ShortString*,uint> GetInterfaceVersionString = (delegate* unmanaged[Cdecl]<ShortString*,uint>)QueryInterface(0x01053fa5);
		public static readonly delegate* unmanaged[Cdecl]<nint*, uint*, uint> EnumPhysicalGPUs = (delegate* unmanaged[Cdecl]<nint*, uint*, uint>)QueryInterface(0xe5ac921f);
		public static readonly delegate* unmanaged[Cdecl]<nint*, void*, uint> I2CRead = (delegate* unmanaged[Cdecl]<nint*, void*, uint>)QueryInterface(0x2fde12c5);
		public static readonly delegate* unmanaged[Cdecl]<nint*, void*, uint> I2CWrite = (delegate* unmanaged[Cdecl]<nint*, void*, uint>)QueryInterface(0xe812eb07);

		public static class Gpu
		{
			public static readonly delegate* unmanaged[Cdecl]<nint, ShortString*, uint> GetFullName = (delegate* unmanaged[Cdecl]<nint, ShortString*, uint>)QueryInterface(0xceee8e9f);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x1be0b8e5);
			public static readonly delegate* unmanaged[Cdecl]<nint, uint*, uint> GetBusSlotId = (delegate* unmanaged[Cdecl]<nint, uint*, uint>)QueryInterface(0x2a0a350f);
			public static readonly delegate* unmanaged[Cdecl]<uint> QueryIlluminationSupport = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xa629da31);
			public static readonly delegate* unmanaged[Cdecl]<uint> GetIllumination = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x9a1b9365);
			public static readonly delegate* unmanaged[Cdecl]<uint> SetIllumination = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x0254a187);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumDevicesGetInfo = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xd4100e58);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumDevicesGetControl = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x73c01d58);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumDevicesSetControl = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x57024c62);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumZonesGetInfo = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x4b81241b);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumZonesGetControl = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x3dbf5764);
			public static readonly delegate* unmanaged[Cdecl]<uint> ClientIllumZonesSetControl = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x197d065e);
		}
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
		uint count;
		ValidateResult(Functions.EnumPhysicalGPUs(array, &count));
		return new Span<PhysicalGpu>((PhysicalGpu*)array, (int)count).ToArray();
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
	}
}

internal sealed class NvApiException : Exception
{
	public uint Status { get; }

	public NvApiException(uint status, string? message) : base(message)
	{
		Status = status;
	}
}
