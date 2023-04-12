namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a static color.</summary>
public readonly struct StaticColorEffect : ISingleColorLightEffect
{
	public RgbColor Color { get; }

	public StaticColorEffect(RgbColor color) => Color = color;
}
