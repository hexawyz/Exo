using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeviceTools.Logitech.HidPlusPlus;

// This class provides information for various parameter types as well as validating them.
// This way, we can prevent invalid parameter types from being used at all, which will avoid reading or writing memory that shouldn't be accessed.
internal static class ParameterInformation<T>
	where T : struct, IMessageParameters
{
	public static readonly SupportedReports SupportedReports = GetSupportedReports();
	public static readonly SupportedReports NativeSupportedReport = SupportedReports switch
	{
		SupportedReports.Short => SupportedReports.Short,
		>= SupportedReports.Long and < SupportedReports.VeryLong => SupportedReports.Long,
		>= SupportedReports.VeryLong => SupportedReports.VeryLong,
		_ => throw new InvalidOperationException()
	};

	public static bool IsShort
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => NativeSupportedReport == SupportedReports.Short;
	}

	public static bool IsLong
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => NativeSupportedReport == SupportedReports.Long;
	}

	public static bool IsVeryLong
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => NativeSupportedReport == SupportedReports.VeryLong;
	}

	public static bool SupportsShort
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => (SupportedReports & SupportedReports.Short) != 0;
	}

	public static bool SupportsLong
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => (SupportedReports & SupportedReports.Long) != 0;
	}

	public static bool SupportsVeryLong
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		get => (SupportedReports & SupportedReports.VeryLong) != 0;
	}

	public static void ThrowIfNoShortSupport()
	{
		if (!SupportsShort) throw new InvalidOperationException($"The parameter type {typeof(T)} does not support short messages.");
	}

	public static void ThrowIfNoLongSupport()
	{
		if (!SupportsLong) throw new InvalidOperationException($"The parameter type {typeof(T)} does not support long messages.");
	}

	public static void ThrowIfNoVeryLongSupport()
	{
		if (!SupportsVeryLong) throw new InvalidOperationException($"The parameter type {typeof(T)} does not support very long messages.");
	}

	public static void ThrowIfNotShort()
	{
		if (!IsShort) throw new InvalidOperationException($"The parameter type {typeof(T)} is not a short parameter type.");
	}

	public static void ThrowIfNotLong()
	{
		if (!IsLong) throw new InvalidOperationException($"The parameter type {typeof(T)} is not a long parameter type.");
	}

	public static void ThrowIfNotVeryLong()
	{
		if (!IsVeryLong) throw new InvalidOperationException($"The parameter type {typeof(T)} is not a very long parameter type.");
	}

	public static Span<byte> GetNativeSpan(ref T parameters)
		=> MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(parameters)), Unsafe.SizeOf<T>());

	public static ReadOnlySpan<byte> GetNativeReadOnlySpan(in T parameters)
		=> MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(parameters)), Unsafe.SizeOf<T>());

	public static ReadOnlySpan<byte> GetShortReadOnlySpan(in T parameters)
	{
		ThrowIfNoShortSupport();
		return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(parameters)), 3);
	}

	public static ReadOnlySpan<byte> GetLongReadOnlySpan(in T parameters)
	{
		ThrowIfNoLongSupport();
		return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(parameters)), 16);
	}

	public static ReadOnlySpan<byte> GetVeryLongReadOnlySpan(in T parameters)
	{
		ThrowIfNoVeryLongSupport();
		return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(parameters)), 60);
	}

	// This should force triggering the static constructor.
	// TODO: Test this.
	public static void ThrowIfInvalid() => _ = SupportedReports;

	private static SupportedReports GetSupportedReports()
	{
		SupportedReports reports = 0;

		if (typeof(IShortMessageParameters).IsAssignableFrom(typeof(T))) reports |= SupportedReports.Short;
		if (typeof(ILongMessageParameters).IsAssignableFrom(typeof(T))) reports |= SupportedReports.Long;
		if (typeof(IVeryLongMessageParameters).IsAssignableFrom(typeof(T))) reports |= SupportedReports.VeryLong;

		int expectedSize;

		if ((reports & SupportedReports.VeryLong) != 0)
		{
			expectedSize = 60;
		}
		else if ((reports & SupportedReports.Long) != 0)
		{
			expectedSize = 16;
		}
		else if ((reports & SupportedReports.Short) != 0)
		{
			expectedSize = 3;
		}
		else
		{
			throw new InvalidOperationException($"The type {typeof(T)} does not implement any parameter size.");
		}

		if (Unsafe.SizeOf<T>() != expectedSize)
		{
			throw new InvalidOperationException($"The type {typeof(T)} should be exactly {expectedSize} bytes long.");
		}

		return reports;
	}
}
