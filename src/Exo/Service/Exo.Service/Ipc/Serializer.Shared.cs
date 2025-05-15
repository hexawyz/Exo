using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Exo.ColorFormats;
using Exo.Cooling;
using Exo.Cooling.Configuration;
using Exo.Images;
using Exo.Lighting;

namespace Exo.Service.Ipc;

internal static class Serializer
{
	public static void Write(ref BufferWriter writer, in Size size)
	{
		writer.Write(size.Width);
		writer.Write(size.Height);
	}

	public static Size ReadSize(ref BufferReader reader)
		=> new(reader.Read<int>(), reader.Read<int>());

	public static void Write(ref BufferWriter writer, in Rectangle rectangle)
	{
		writer.Write(rectangle.Left);
		writer.Write(rectangle.Top);
		writer.Write(rectangle.Width);
		writer.Write(rectangle.Height);
	}

	public static Rectangle ReadRectangle(ref BufferReader reader)
		=> new(reader.Read<int>(), reader.Read<int>(), reader.Read<int>(), reader.Read<int>());

	public static void Write(ref BufferWriter writer, ImmutableArray<DotsPerInch> dpiArray)
	{
		if (dpiArray.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)dpiArray.Length);
			foreach (var dpi in dpiArray)
			{
				Write(ref writer, in dpi);
			}
		}
	}

	public static void Write(ref BufferWriter writer, in DotsPerInch dpi)
	{
		writer.Write(dpi.Horizontal);
		writer.Write(dpi.Vertical);
	}

	public static void Write(ref BufferWriter writer, in RgbColor color)
	{
		writer.Write(color.R);
		writer.Write(color.G);
		writer.Write(color.B);
	}

	public static RgbColor ReadRgbColor(ref BufferReader reader)
		=> new(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());

	public static ImmutableArray<DotsPerInch> ReadDotsPerInches(ref BufferReader reader)
	{
		uint count = reader.ReadVariableUInt32();
		if (count == 0) return [];
		var dpiArray = new DotsPerInch[count];
		for (int i = 0; i < dpiArray.Length; i++)
		{
			dpiArray[i] = ReadDotsPerInch(ref reader);
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(dpiArray);
	}

	public static DotsPerInch ReadDotsPerInch(ref BufferReader reader)
		=> new(reader.Read<ushort>(), reader.Read<ushort>());

	public static void Write(ref BufferWriter writer, LightingEffect? effect)
	{
		if (effect is not null)
		{
			System.Diagnostics.Debug.Assert(effect.EffectId != default);
			writer.Write(effect.EffectId);
			writer.WriteVariable((uint)effect.EffectData.Length);
			writer.Write(effect.EffectData);
		}
		else
		{
			writer.Write(Guid.Empty);
		}
	}

	public static LightingEffect? ReadLightingEffect(ref BufferReader reader)
	{
		var effectId = reader.ReadGuid();
		if (effectId == default) return null;

		uint length = reader.ReadVariableUInt32();

		var data = new byte[length];
		reader.Read(data);
		return new(effectId, data);
	}

	public static void Write(ref BufferWriter writer, CoolingControlCurveConfiguration controlCurve)
	{
		switch (controlCurve)
		{
		case CoolingControlCurveConfiguration<byte> cc:
			writer.Write((byte)SensorDataType.UInt8);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<ushort> cc:
			writer.Write((byte)SensorDataType.UInt16);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<uint> cc:
			writer.Write((byte)SensorDataType.UInt32);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<ulong> cc:
			writer.Write((byte)SensorDataType.UInt64);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<UInt128> cc:
			writer.Write((byte)SensorDataType.UInt128);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<sbyte> cc:
			writer.Write((byte)SensorDataType.SInt8);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<short> cc:
			writer.Write((byte)SensorDataType.SInt16);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<int> cc:
			writer.Write((byte)SensorDataType.SInt32);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<long> cc:
			writer.Write((byte)SensorDataType.SInt64);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<Int128> cc:
			writer.Write((byte)SensorDataType.SInt128);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<Half> cc:
			writer.Write((byte)SensorDataType.Float16);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<float> cc:
			writer.Write((byte)SensorDataType.Float32);
			Write(ref writer, cc);
			break;
		case CoolingControlCurveConfiguration<double> cc:
			writer.Write((byte)SensorDataType.Float64);
			Write(ref writer, cc);
			break;
		default:
			throw new NotImplementedException();
		}
	}

	private static void Write<T>(ref BufferWriter writer, CoolingControlCurveConfiguration<T> controlCurve)
		where T : unmanaged, INumber<T>
	{
		Write(ref writer, controlCurve.Points);
		writer.Write(controlCurve.InitialValue);
	}

	private static void Write<T>(ref BufferWriter writer, ImmutableArray<DataPoint<T, byte>> points)
		where T : unmanaged, INumber<T>
	{
		if (points.IsDefaultOrEmpty)
		{
			writer.Write((byte)0);
		}
		else
		{
			writer.WriteVariable((uint)points.Length);
			foreach (var point in points)
			{
				Write(ref writer, point);
			}
		}
	}

	private static void Write<T>(ref BufferWriter writer, DataPoint<T, byte> point)
		where T : unmanaged, INumber<T>
	{
		writer.Write(point.X);
		writer.Write(point.Y);
	}

	public static CoolingControlCurveConfiguration ReadControlCurve(ref BufferReader reader)
		=> (SensorDataType)reader.ReadByte() switch
		{
			SensorDataType.UInt8 => ReadControlCurve<byte>(ref reader),
			SensorDataType.UInt16 => ReadControlCurve<ushort>(ref reader),
			SensorDataType.UInt32 => ReadControlCurve<uint>(ref reader),
			SensorDataType.UInt64 => ReadControlCurve<ulong>(ref reader),
			SensorDataType.UInt128 => ReadControlCurve<UInt128>(ref reader),
			SensorDataType.SInt8 => ReadControlCurve<byte>(ref reader),
			SensorDataType.SInt16 => ReadControlCurve<short>(ref reader),
			SensorDataType.SInt32 => ReadControlCurve<int>(ref reader),
			SensorDataType.SInt64 => ReadControlCurve<long>(ref reader),
			SensorDataType.SInt128 => ReadControlCurve<Int128>(ref reader),
			SensorDataType.Float16 => ReadControlCurve<Half>(ref reader),
			SensorDataType.Float32 => ReadControlCurve<float>(ref reader),
			SensorDataType.Float64 => ReadControlCurve<double>(ref reader),
			_ => throw new NotSupportedException(),
		};

	private static CoolingControlCurveConfiguration<T> ReadControlCurve<T>(ref BufferReader reader)
		where T : unmanaged, INumber<T>
		=> new(ReadDataPoints<T>(ref reader), reader.ReadByte());

	private static ImmutableArray<DataPoint<T, byte>> ReadDataPoints<T>(ref BufferReader reader)
		where T : unmanaged, INumber<T>
	{
		uint count = reader.ReadVariableUInt32();
		if (count == 0) return [];
		var points = new DataPoint<T, byte>[count];
		for (int i = 0; i < points.Length; i++)
		{
			points[i] = ReadDataPoint<T>(ref reader);
		}
		return ImmutableCollectionsMarshal.AsImmutableArray(points);
	}

	private static DataPoint<T, byte> ReadDataPoint<T>(ref BufferReader reader)
		where T : unmanaged, INumber<T>
		=> new(reader.Read<T>(), reader.ReadByte());
}
