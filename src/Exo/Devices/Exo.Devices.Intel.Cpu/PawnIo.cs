using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace Exo.Devices.Intel.Cpu;

[SuppressUnmanagedCodeSecurity]
internal sealed class PawnIo : IDisposable
{
	private static readonly bool IsLibraryLoaded;
	private static readonly uint LibraryVersion;

	static PawnIo()
	{
		// We should't hold a permanent handle on the library, so the trick is to call the version API after load to force the runtime to acquire a handle itself.
		if (TryLoadLibrary(out nint handle))
		{
			try
			{
				uint version = 0;
				uint result = pawnio_version(out version);
				if (result != 0) Marshal.ThrowExceptionForHR((int)result);
				LibraryVersion = version;
				IsLibraryLoaded = true;
			}
			finally
			{
				NativeLibrary.Free(handle);
			}
		}
	}

	[DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = true)]
	private static extern uint pawnio_version(out uint version);

	[DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = true)]
	private static extern uint pawnio_open(out nint handle);

	[DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = true)]
	private static extern unsafe uint pawnio_load(nint handle, byte* blob, nint size);

	[DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = true)]
	private static extern unsafe uint pawnio_execute
	(
		nint handle,
		byte* name,
		ulong* in_array,
		nint in_size,
		ulong* out_array,
		nint out_size,
		nint* return_size
	);

	[DllImport("PawnIOLib", PreserveSig = false)]
	private static extern uint pawnio_close(nint handle);

	private nint _handle;

	private static bool TryLoadLibrary(out nint handle)
	{
		// There might be a problem with the installer, as on my system, the registry key ended up in the WoW64 world…
		if ((Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\PawnIO", "Install_Dir", null) ??
			Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PawnIO", "Install_Dir", null)) is string { Length: > 0 } pawnIoPath)
		{
			try
			{
				handle = NativeLibrary.Load(Path.Combine(pawnIoPath, "PawnIOLib"));
				return true;
			}
			catch
			{
			}
		}

		handle = default;
		return false;
	}

	public PawnIo()
	{
		if (!IsLibraryLoaded) throw new DllNotFoundException("PawnIOLib.dll was not found during startup.");
		uint result = pawnio_open(out _handle);
		if (result != 0) Marshal.ThrowExceptionForHR((int)result);
	}

	~PawnIo() => Dispose(false);

	public static uint Version
	{
		get
		{
			if (!IsLibraryLoaded) throw new DllNotFoundException("PawnIOLib.dll was not found during startup.");
			return Version;
		}
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public void Dispose(bool disposing)
	{
		nint handle = Interlocked.Exchange(ref _handle, 0);
		if (handle != 0) pawnio_close(_handle);
	}

	private nint Handle
	{
		get
		{
			nint handle = Volatile.Read(ref _handle);
			ObjectDisposedException.ThrowIf(handle == 0, this);
			return handle;
		}
	}

	public unsafe void LoadModule(ReadOnlySpan<byte> data)
	{
		fixed (byte* dataPointer = data)
		{
			uint result = pawnio_load(_handle, dataPointer, (nint)(uint)data.Length);
			if (result != 0) Marshal.ThrowExceptionForHR((int)result);
		}
	}

	public unsafe void LoadModuleFromResource(Assembly assembly, string resourceName)
	{
		using (var s = assembly.GetManifestResourceStream(resourceName))
		{
			if (s is not UnmanagedMemoryStream ums) throw new InvalidOperationException();
			unsafe
			{
				uint result = pawnio_load(_handle, ums.PositionPointer, (nint)ums.Length);
				if (result != 0) Marshal.ThrowExceptionForHR((int)result);
			}
		}
	}

	// NB: In many cases, we shouldn't even need to use fixed at all. UTF-8 literals would be pinned by design, and both inputs and outputs would come from stack.
	public unsafe nint Execute(ReadOnlySpan<byte> nullTerminatedName, ReadOnlySpan<ulong> input, Span<ulong> output)
	{
		nint length = 0;
		fixed (byte* namePointer = nullTerminatedName)
		fixed (ulong* inputPointer = input)
		fixed (ulong* outputPointer = output)
		{
			uint result = pawnio_execute
			(
				Handle,
				namePointer,
				inputPointer,
				(nint)(uint)input.Length,
				outputPointer,
				(nint)(uint)output.Length,
				&length
			);
			if (result != 0) Marshal.ThrowExceptionForHR((int)result);
		}
		return length;
	}
}

