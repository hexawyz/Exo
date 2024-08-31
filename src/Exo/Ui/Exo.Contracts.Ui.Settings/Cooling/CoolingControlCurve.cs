using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings.Cooling;

[DataContract]
public sealed class CoolingControlCurve
{
	private readonly object? _value;

	[IgnoreDataMember]
	public object? RawValue => _value;

	[DataMember(Order = 1)]
	public UnsignedIntegerCoolingControlCurve? UnsignedInteger
	{
		get => _value as UnsignedIntegerCoolingControlCurve;
		init => _value = value;
	}

	[DataMember(Order = 2)]
	public SignedIntegerCoolingControlCurve? SignedInteger
	{
		get => _value as SignedIntegerCoolingControlCurve;
		init => _value = value;
	}

	[DataMember(Order = 3)]
	public SinglePrecisionFloatingPointCoolingControlCurve? SinglePrecisionFloatingPoint
	{
		get => _value as SinglePrecisionFloatingPointCoolingControlCurve;
		init => _value = value;
	}

	[DataMember(Order = 4)]
	public DoublePrecisionFloatingPointCoolingControlCurve? DoublePrecisionFloatingPoint
	{
		get => _value as DoublePrecisionFloatingPointCoolingControlCurve;
		init => _value = value;
	}
}
