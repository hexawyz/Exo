using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Exo.Cooling.Configuration;

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "dataType", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<sbyte>), "SInt8")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<byte>), "UInt8")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<short>), "SInt16")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<ushort>), "UInt16")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<int>), "SInt32")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<uint>), "UInt32")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<long>), "SInt64")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<ulong>), "UInt64")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<Half>), "Float16")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<float>), "Float32")]
[JsonDerivedType(typeof(CoolingControlCurveConfiguration<double>), "Float64")]
public abstract class CoolingControlCurveConfiguration
{
	private protected CoolingControlCurveConfiguration() { }

	public virtual byte InitialValue { get; }
}

[method: JsonConstructor]
public sealed class CoolingControlCurveConfiguration<T>(ImmutableArray<DataPoint<T, byte>> points, byte initialValue) : CoolingControlCurveConfiguration
	where T : struct, INumber<T>
{
	private readonly ImmutableArray<DataPoint<T, byte>> _points = points;
	private readonly byte _initialValue = initialValue;

	public ImmutableArray<DataPoint<T, byte>> Points => _points;

	public override byte InitialValue => _initialValue;
}
