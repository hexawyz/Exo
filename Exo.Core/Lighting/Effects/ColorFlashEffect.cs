namespace Exo.Lighting.Effects;

/// <summary>Represents a light with a flashing color effect.</summary>
public readonly struct ColorFlashEffect : ISingleColorLightEffect
{
	public RgbColor Color { get; }

	public ColorFlashEffect(RgbColor color) => Color = color;
}
