using System.ComponentModel;
using System.Runtime.Serialization;

namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a static color.</summary>
[DataContract]
[EffectName("Static Color")]
public readonly struct StaticColorEffect : ISingleColorLightEffect
{
	[DataMember(Order = 1)]
	[DisplayName("Color")]
	public RgbColor Color { get; }

	public StaticColorEffect(RgbColor color) => Color = color;
}
