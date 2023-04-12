namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a pulsing color effect.</summary>
public readonly struct ColorPulseEffect : ISingleColorLightEffect
{
	public RgbColor Color { get; }

	public ColorPulseEffect(RgbColor color) => Color = color;
}
