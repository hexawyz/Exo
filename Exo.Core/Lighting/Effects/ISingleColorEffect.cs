namespace Exo.Lighting.Effects;

public interface ISingleColorLightEffect : ILightingEffect
{
	RgbColor Color { get; }
}
