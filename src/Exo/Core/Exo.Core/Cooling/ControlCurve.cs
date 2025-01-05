using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Exo.Cooling;

public delegate bool MonotonicityValidator<TValue>(TValue a, TValue b) where TValue : struct, INumber<TValue>;

public sealed class MonotonicityValidators<TValue>
	where TValue : struct, INumber<TValue>
{
	private static readonly TValue OneHundred = TValue.CreateChecked((byte)100);

	private static readonly MonotonicityValidators<TValue> Instance = new();

	public static readonly MonotonicityValidator<TValue> Any = Instance.ValidateAny;
	public static readonly MonotonicityValidator<TValue> Increasing = Instance.ValidateIncreasing;
	public static readonly MonotonicityValidator<TValue> Decreasing = Instance.ValidateDecreasing;
	public static readonly MonotonicityValidator<TValue> StrictlyIncreasing = Instance.ValidateStrictlyIncreasing;
	public static readonly MonotonicityValidator<TValue> StrictlyDecreasing = Instance.ValidateStrictlyDecreasing;

	public static readonly MonotonicityValidator<TValue> IncreasingUpTo100 = Instance.ValidateIncreasingUpTo100;
	public static readonly MonotonicityValidator<TValue> StrictlyIncreasingUpTo100 = Instance.ValidateStrictlyIncreasingUpTo100;

	private MonotonicityValidators() { }

	private bool ValidateAny(TValue a, TValue b)
		=> !(TValue.IsNaN(a) || TValue.IsNaN(b));

	private bool ValidateIncreasing(TValue a, TValue b)
		=> a <= b;

	private bool ValidateDecreasing(TValue a, TValue b)
		=> a >= b;

	private bool ValidateStrictlyIncreasing(TValue a, TValue b)
		=> a < b;

	private bool ValidateStrictlyDecreasing(TValue a, TValue b)
		=> a > b;

	private bool ValidateIncreasingUpTo100(TValue a, TValue b)
		=> a <= b && b <= TValue.CreateChecked((byte)100);

	private bool ValidateStrictlyIncreasingUpTo100(TValue a, TValue b)
		=> a < b && b <= TValue.CreateChecked((byte)100);
}

public sealed class InterpolatedSegmentControlCurve<TInput, TOutput> : IControlCurve<TInput, TOutput>
	where TInput : struct, INumber<TInput>
	where TOutput : struct, INumber<TOutput>
{
	private static void ValidatePoints(ImmutableArray<DataPoint<TInput, TOutput>> points, MonotonicityValidator<TOutput> monotonicityValidator)
	{
		if (points.IsDefault) throw new ArgumentNullException(nameof(points));
		if (points.IsEmpty) throw new ArgumentException(nameof(points));
		var lastPoint = points[0];
		if (TInput.IsNaN(lastPoint.X) || !TOutput.IsRealNumber(lastPoint.Y)) throw new ArgumentException("Points must have a valid value.", nameof(points));
		for (int i = 1; i < points.Length; i++)
		{
			var currentPoint = points[i];
			if (TInput.IsNaN(lastPoint.X) || !TOutput.IsRealNumber(lastPoint.Y)) throw new ArgumentException("Points must have a valid value.", nameof(points));
			if (currentPoint.X < lastPoint.X) throw new ArgumentException("Points must be ordered based on X coordinate.", nameof(points));
			if (!monotonicityValidator(lastPoint.Y, currentPoint.Y)) throw new ArgumentException("Points must respect the required monotonicity.", nameof(points));
			lastPoint = currentPoint;
		}
	}

	private readonly ImmutableArray<DataPoint<TInput, TOutput>> _points;
	private readonly TOutput _initialValue;

	public InterpolatedSegmentControlCurve(ImmutableArray<DataPoint<TInput, TOutput>> points, MonotonicityValidator<TOutput> monotonicityValidator)
	{
		ValidatePoints(points, monotonicityValidator);
		_points = points;
		_initialValue = points[0].Y;
	}

	public InterpolatedSegmentControlCurve(ImmutableArray<DataPoint<TInput, TOutput>> points, TOutput initialValue, MonotonicityValidator<TOutput> monotonicityValidator)
	{
		ValidatePoints(points, monotonicityValidator);
		if (!TOutput.IsRealNumber(initialValue)) throw new ArgumentException("Initial value must be a real number.", nameof(initialValue));
		var firstY = points[0].Y;
		// NB: We intentionally allow the initial value setting to be the same as the first point even though the monotonicity check might be strict.
		// This is in line with the default behavior of using the first point as a the initial value if not specified.
		if (initialValue != firstY && !monotonicityValidator(initialValue, firstY)) throw new ArgumentException("Initial value must be equal to the first value, or be ordered with the required monotonicity.", nameof(points));
		_points = points;
		_initialValue = initialValue;
	}

	private InterpolatedSegmentControlCurve(ImmutableArray<DataPoint<TInput, TOutput>> points, TOutput initialValue)
	{
		_points = points;
		_initialValue = initialValue;
	}

	public TOutput this[TInput value]
	{
		get
		{
			var previousPoint = _points[0];

			if (value < previousPoint.X) return _initialValue;

			var nextPoint = previousPoint;
			for (int i = 1; i < _points.Length; i++)
			{
				nextPoint = _points[i];
				if (value < nextPoint.X) goto Interpolate;
				previousPoint = nextPoint;
			}
			return nextPoint.Y;
		Interpolate:;
			return DataPointInterpolator.Get<TInput, TOutput>().Interpolate(value, previousPoint, nextPoint);
		}
	}

	/// <summary>Remaps the curve to other compatible input and/or output types.</summary>
	/// <remarks>
	/// This operation will fail if any of the points of the current curve fail to be converted.
	/// It should generally be quicker than trying to do the same manually, as this method will not revalidate the points.
	/// </remarks>
	/// <typeparam name="TOtherInput">Input number type for the new curve.</typeparam>
	/// <typeparam name="TOtherOutput">Output number type for the new curve</typeparam>
	/// <returns></returns>
	public InterpolatedSegmentControlCurve<TOtherInput, TOtherOutput> Cast<TOtherInput, TOtherOutput>()
		where TOtherInput : struct, INumber<TOtherInput>
		where TOtherOutput : struct, INumber<TOtherOutput>
	{
		if (typeof(TOtherInput) == typeof(TInput) && typeof(TOtherOutput) == typeof(TOutput)) return Unsafe.As<InterpolatedSegmentControlCurve<TOtherInput, TOtherOutput>>(this);
		else if (typeof(TOtherInput) == typeof(TInput)) return Unsafe.As<InterpolatedSegmentControlCurve<TOtherInput, TOtherOutput>>(CastOutput<TOtherOutput>());
		else if (typeof(TOtherOutput) == typeof(TOutput)) return Unsafe.As<InterpolatedSegmentControlCurve<TOtherInput, TOtherOutput>>(CastInput<TOtherOutput>());

		var initialValue = TOtherOutput.CreateChecked(_initialValue);
		var dataPoints = ImmutableArray.CreateRange(_points, static p => new DataPoint<TOtherInput, TOtherOutput>(TOtherInput.CreateChecked(p.X), TOtherOutput.CreateChecked(p.Y)));
		return new(dataPoints, initialValue);
	}

	/// <summary>Remaps the curve to another compatible input type.</summary>
	/// <remarks>
	/// This operation will fail if any of the points of the current curve fail to be converted.
	/// It should generally be quicker than trying to do the same manually, as this method will not revalidate the points.
	/// </remarks>
	/// <typeparam name="TOtherInput">Input number type for the new curve.</typeparam>
	/// <returns></returns>
	public InterpolatedSegmentControlCurve<TOtherInput, TOutput> CastInput<TOtherInput>()
		where TOtherInput : struct, INumber<TOtherInput>
	{
		if (typeof(TOtherInput) == typeof(TInput)) return Unsafe.As<InterpolatedSegmentControlCurve<TOtherInput, TOutput>>(this);

		var dataPoints = ImmutableArray.CreateRange(_points, static p => new DataPoint<TOtherInput, TOutput>(TOtherInput.CreateChecked(p.X), p.Y));
		return new(dataPoints, _initialValue);
	}

	/// <summary>Remaps the curve to another compatible output type.</summary>
	/// <remarks>
	/// This operation will fail if any of the points of the current curve fail to be converted.
	/// It should generally be quicker than trying to do the same manually, as this method will not revalidate the points.
	/// </remarks>
	/// <typeparam name="TOtherOutput">Output number type for the new curve</typeparam>
	/// <returns></returns>
	public InterpolatedSegmentControlCurve<TInput, TOtherOutput> CastOutput<TOtherOutput>()
		where TOtherOutput : struct, INumber<TOtherOutput>
	{
		if (typeof(TOtherOutput) == typeof(TOutput)) return Unsafe.As<InterpolatedSegmentControlCurve<TInput, TOtherOutput>>(this);

		var initialValue = TOtherOutput.CreateChecked(_initialValue);
		var dataPoints = ImmutableArray.CreateRange(_points, static p => new DataPoint<TInput, TOtherOutput>(p.X, TOtherOutput.CreateChecked(p.Y)));
		return new(dataPoints, initialValue);
	}

	public ImmutableArray<DataPoint<TInput, TOutput>> GetPoints() => _points;
}

public interface IDataPointInterpolator<TX, TY>
	where TX : struct, INumber<TX>
	where TY : struct, INumber<TY>
{
	TY Interpolate(TX x, DataPoint<TX, TY> a, DataPoint<TX, TY> b);
}

public sealed class DataPointInterpolator
{
	private sealed class Cache<TX, TY>
		where TX : struct, INumber<TX>
		where TY : struct, INumber<TY>
	{
		public static IDataPointInterpolator<TX, TY> Value = Create<TX, TY>();
	}

	private static IDataPointInterpolator<TX, TY> Create<TX, TY>()
		where TX : struct, INumber<TX>
		where TY : struct, INumber<TY>
	{
		// NB: Interpolations for byte are hardcoded for now, but generally speaking, it should be possible to defer all small values to the Int32 to Byte code with a generic adapter.
		if (typeof(TY) == typeof(byte))
		{
			if (typeof(TX) == typeof(sbyte))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(SByteToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(byte))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(ByteToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(ushort))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(UInt16ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(short))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(Int16ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(uint))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(UInt32ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(int))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(Int32ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(ulong))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(UInt64ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(long))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(Int64ToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(Half))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(SingleToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(float))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(SingleToByteDataPointInterpolator.Instance);
			}
			else if (typeof(TX) == typeof(double))
			{
				return Unsafe.As<IDataPointInterpolator<TX, TY>>(DoubleToByteDataPointInterpolator.Instance);
			}
		}
		// TODO: Implement interpolation for all types.
		// By using double, it should be possible to have mostly generic code for handing cases, with a forced fallback to double values, but more efficient code should be doable in most cases.
		throw new NotSupportedException($"Support for interpolating from {typeof(TX)} to {typeof(TY)} is not implemented yet.");
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IDataPointInterpolator<TX, TY> Get<TX, TY>()
		where TX : struct, INumber<TX>
		where TY : struct, INumber<TY>
		=> Cache<TX, TY>.Value;
}

public sealed class SByteToByteDataPointInterpolator : IDataPointInterpolator<sbyte, byte>
{
	public static readonly SByteToByteDataPointInterpolator Instance = new SByteToByteDataPointInterpolator();

	private SByteToByteDataPointInterpolator() { }

	public byte Interpolate(sbyte x, DataPoint<sbyte, byte> a, DataPoint<sbyte, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class ByteToByteDataPointInterpolator : IDataPointInterpolator<byte, byte>
{
	public static readonly ByteToByteDataPointInterpolator Instance = new ByteToByteDataPointInterpolator();

	private ByteToByteDataPointInterpolator() { }

	public byte Interpolate(byte x, DataPoint<byte, byte> a, DataPoint<byte, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class Int16ToByteDataPointInterpolator : IDataPointInterpolator<short, byte>
{
	public static readonly Int16ToByteDataPointInterpolator Instance = new Int16ToByteDataPointInterpolator();

	private Int16ToByteDataPointInterpolator() { }

	public byte Interpolate(short x, DataPoint<short, byte> a, DataPoint<short, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class UInt16ToByteDataPointInterpolator : IDataPointInterpolator<ushort, byte>
{
	public static readonly UInt16ToByteDataPointInterpolator Instance = new UInt16ToByteDataPointInterpolator();

	private UInt16ToByteDataPointInterpolator() { }

	public byte Interpolate(ushort x, DataPoint<ushort, byte> a, DataPoint<ushort, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class Int32ToByteDataPointInterpolator : IDataPointInterpolator<int, byte>
{
	public static readonly Int32ToByteDataPointInterpolator Instance = new Int32ToByteDataPointInterpolator();

	private Int32ToByteDataPointInterpolator() { }

	public byte Interpolate(int x, DataPoint<int, byte> a, DataPoint<int, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class UInt32ToByteDataPointInterpolator : IDataPointInterpolator<uint, byte>
{
	public static readonly UInt32ToByteDataPointInterpolator Instance = new UInt32ToByteDataPointInterpolator();

	private UInt32ToByteDataPointInterpolator() { }

	public byte Interpolate(uint x, DataPoint<uint, byte> a, DataPoint<uint, byte> b)
		=> byte.CreateSaturating(a.Y + ((long)x - a.X) * (b.Y - a.Y) / ((long)b.X - a.X));
}

public sealed class Int64ToByteDataPointInterpolator : IDataPointInterpolator<long, byte>
{
	public static readonly Int64ToByteDataPointInterpolator Instance = new Int64ToByteDataPointInterpolator();

	private Int64ToByteDataPointInterpolator() { }

	public byte Interpolate(long x, DataPoint<long, byte> a, DataPoint<long, byte> b)
		=> byte.CreateSaturating(a.Y + (x - a.X) * (b.Y - a.Y) / (b.X - a.X));
}

public sealed class UInt64ToByteDataPointInterpolator : IDataPointInterpolator<ulong, byte>
{
	public static readonly UInt64ToByteDataPointInterpolator Instance = new UInt64ToByteDataPointInterpolator();

	private UInt64ToByteDataPointInterpolator() { }

	// NB: It should be possible to do better in this case, but for now, we basically apply the "generic" algorithm of fallback to double for simplicity.
	public byte Interpolate(ulong x, DataPoint<ulong, byte> a, DataPoint<ulong, byte> b)
		=> DoubleToByteDataPointInterpolator.Instance.Interpolate(x, new(a.X, a.Y), new(b.X, b.Y));
}

public sealed class HalfToByteDataPointInterpolator : IDataPointInterpolator<Half, byte>
{
	public static readonly HalfToByteDataPointInterpolator Instance = new HalfToByteDataPointInterpolator();

	private HalfToByteDataPointInterpolator() { }

	// NB: There is no hardware interpolation support for Half at the moment, so falling back to float should be the better choice.
	public byte Interpolate(Half x, DataPoint<Half, byte> a, DataPoint<Half, byte> b)
		=> SingleToByteDataPointInterpolator.Instance.Interpolate((float)x, new((float)a.X, a.Y), new((float)b.X, b.Y));
}

public sealed class SingleToByteDataPointInterpolator : IDataPointInterpolator<float, byte>
{
	public static readonly SingleToByteDataPointInterpolator Instance = new SingleToByteDataPointInterpolator();

	private SingleToByteDataPointInterpolator() { }

	public byte Interpolate(float x, DataPoint<float, byte> a, DataPoint<float, byte> b)
		=> byte.CreateSaturating(a.Y + int.CreateSaturating((x - a.X) * (b.Y - a.Y) / (b.X - a.X)));
}

public sealed class DoubleToByteDataPointInterpolator : IDataPointInterpolator<double, byte>
{
	public static readonly DoubleToByteDataPointInterpolator Instance = new DoubleToByteDataPointInterpolator();

	private DoubleToByteDataPointInterpolator() { }

	public byte Interpolate(double x, DataPoint<double, byte> a, DataPoint<double, byte> b)
		=> byte.CreateSaturating(a.Y + int.CreateSaturating((x - a.X) * (b.Y - a.Y) / (b.X - a.X)));
}

internal static class NumberInfo
{
	private static class Cache<T>
		where T : struct, INumber<T>
	{
		public static readonly TypeDetails Details = GetDetails();

		private static TypeDetails GetDetails()
		{
			TypeDetails details = 0;
			foreach (var interfaceType in typeof(T).GetInterfaces())
			{
				if (!interfaceType.IsGenericType) continue;
				var genericInterfaceType = interfaceType.GetGenericTypeDefinition();
				if (genericInterfaceType == typeof(ISignedNumber<>))
				{
					details |= TypeDetails.Signed;
				}
				else if (genericInterfaceType == typeof(IMinMaxValue<>))
				{
					details |= TypeDetails.MinMax;
				}
				else if (genericInterfaceType == typeof(IFloatingPoint<>))
				{
					details |= TypeDetails.FloatingPoint;
				}
				else if (genericInterfaceType == typeof(IFloatingPointIeee754<>))
				{
					details |= TypeDetails.FloatingPointIeee754;
				}
			}
			if ((details & (TypeDetails.Signed | TypeDetails.MinMax)) == (TypeDetails.Signed | TypeDetails.MinMax))
			{
				if ((bool)CheckIfNegativeMaxIsAllowedMethodInfo.MakeGenericMethod(typeof(IMinMaxValue<>).MakeGenericType(typeof(T))).Invoke(null, null)!)
				{
					details |= TypeDetails.NegativeMax;
				}
			}

			return details;
		}
	}

	[Flags]
	private enum TypeDetails
	{
		None = 0,
		Signed = 0b0000001,
		MinMax = 0b0000010,
		NegativeMax = 0b0000100,
		FloatingPoint = 0b0001000,
		FloatingPointIeee754 = 0b0010000,
	}

	private static readonly MethodInfo CheckIfNegativeMaxIsAllowedMethodInfo = typeof(NumberInfo).GetMethod("CheckIfNegativeMaxIsAllowed`1", BindingFlags.Static | BindingFlags.NonPublic)!;

	private static bool CheckIfNegativeMaxIsAllowed<T>() where T : struct, INumber<T>, ISignedNumber<T>, IMinMaxValue<T>
	{
		try
		{
			return T.Abs(-T.MaxValue) == T.MaxValue;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsSigned<T>() where T : struct, INumber<T> => (Cache<T>.Details | TypeDetails.Signed) != 0;
	public static bool HasMinMax<T>() where T : struct, INumber<T> => (Cache<T>.Details | TypeDetails.MinMax) != 0;
	public static bool HasNegativeMax<T>() where T : struct, INumber<T> => (Cache<T>.Details | TypeDetails.NegativeMax) != 0;
	public static bool IsFloatingPoint<T>() where T : struct, INumber<T> => (Cache<T>.Details | TypeDetails.FloatingPoint) != 0;
	public static bool IsFloatingPointIeee754<T>() where T : struct, INumber<T> => (Cache<T>.Details | TypeDetails.FloatingPointIeee754) != 0;
}
