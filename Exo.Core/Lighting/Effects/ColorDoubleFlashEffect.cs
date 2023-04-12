namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a double-flashing color effect.</summary>
public readonly struct ColorDoubleFlashEffect : ISingleColorLightEffect
{
	public RgbColor Color { get; }

	public ColorDoubleFlashEffect(RgbColor color) => Color = color;
}
