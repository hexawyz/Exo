using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Exo.Cooling.Configuration;

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "dataType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CoolingCurveConfiguration<sbyte>), "SInt8")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<byte>), "UInt8")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<short>), "SInt16")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<ushort>), "UInt16")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<int>), "SInt32")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<uint>), "UInt32")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<long>), "SInt64")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<ulong>), "UInt64")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<Half>), "Float16")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<float>), "Float32")]
[JsonDerivedType(typeof(CoolingCurveConfiguration<double>), "Float64")]
public abstract class CoolingCurveConfiguration
{
	private protected CoolingCurveConfiguration() { }
}

public sealed class CoolingCurveConfiguration<T> : CoolingCurveConfiguration
	where T : struct, INumber<T>
{
	private readonly ImmutableArray<DataPoint<T, byte>> _points = [];

	public required ImmutableArray<DataPoint<T, byte>> Points
	{
		get => _points;
		init => _points = value.IsDefaultOrEmpty ? [] : value;
	}
}
