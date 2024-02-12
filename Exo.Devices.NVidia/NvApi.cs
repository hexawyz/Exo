using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Exo.Devices.NVidia;

internal unsafe sealed class NvApi
{
	private static readonly NvApi Instance = new();

	static NvApi() { }

	private static class Functions
	{
		public static readonly delegate* unmanaged[Cdecl]<uint> Initialize = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0x0150e828);
		public static readonly delegate* unmanaged[Cdecl]<uint> Unload = (delegate* unmanaged[Cdecl]<uint>)QueryInterface(0xd22bdd7e);
		public static readonly delegate* unmanaged[Cdecl]<uint, byte*,uint> GetErrorMessage = (delegate* unmanaged[Cdecl]<uint, byte*,uint>)QueryInterface(0x6c2d048c);
		public static readonly delegate* unmanaged[Cdecl]<byte*,uint> GetInterfaceVersionString = (delegate* unmanaged[Cdecl]<byte*,uint>)QueryInterface(0x01053fa5);
	}

	private NvApi()
	{
		Functions.Initialize();
	}

	~NvApi()
	{
		Functions.Unload();
	}

	[DllImport("nvapi64", EntryPoint = "nvapi_QueryInterface", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
	public static extern void* QueryInterface(uint functionId);

	public static string GetInterfaceVersionString()
	{
		ShortString str = default;

		if (Functions.GetInterfaceVersionString((byte*)&str) != 0)
		{
			return null!;
		}
		return str.ToString();
	}

	public static string? GetErrorMessage(uint status)
	{
		ShortString str = default;

		if (Functions.GetErrorMessage(status, (byte*)&str) != 0)
		{
			return null;
		}
		return str.ToString();
	}

	public struct ShortString
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
}
