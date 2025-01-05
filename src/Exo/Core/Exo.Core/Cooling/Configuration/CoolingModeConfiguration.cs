using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Exo.Cooling.Configuration;

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "name", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(AutomaticCoolingModeConfiguration), "Automatic")]
[JsonDerivedType(typeof(FixedCoolingModeConfiguration), "Fixed")]
[JsonDerivedType(typeof(SoftwareCurveCoolingModeConfiguration), "SoftwareCurve")]
[JsonDerivedType(typeof(HardwareCurveCoolingModeConfiguration), "HardwareCurve")]
public abstract class CoolingModeConfiguration
{
	private protected CoolingModeConfiguration() { }
}

public sealed class AutomaticCoolingModeConfiguration : CoolingModeConfiguration { }

public sealed class FixedCoolingModeConfiguration : CoolingModeConfiguration
{
	private readonly byte _power;

	[Range(0, 100)]
	public required byte Power
	{
		get => _power;
		init
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
			_power = value;
		}
	}
}

public sealed class SoftwareCurveCoolingModeConfiguration : CoolingModeConfiguration
{
	public required Guid SensorDeviceId { get; init; }
	public required Guid SensorId { get; init; }
	private readonly byte _defaultPower;

	[Range(0, 100)]
	public byte DefaultPower
	{
		get => _defaultPower;
		init
		{
			ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100);
			_defaultPower = value;
		}
	}

	public required CoolingCurveConfiguration Curve { get; init; }
}

public sealed class HardwareCurveCoolingModeConfiguration : CoolingModeConfiguration
{
	public required Guid SensorId { get; init; }
	public required CoolingCurveConfiguration Curve { get; init; }
}
