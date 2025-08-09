namespace Exo.Lighting;

/// <summary>Defines a frame to be used for an addressable lighting effect.</summary>
/// <typeparam name="TColor">The type of color items supported by the lighting zone.</typeparam>
/// <param name="colors">The colors to be used for that frame.</param>
/// <param name="duration">The duration for which the frame must be shown.</param>
public readonly struct LightingEffectFrame<TColor>(Memory<TColor> colors, ushort duration)
	where TColor : unmanaged
{
	public Memory<TColor> Colors { get; } = colors;
	public ushort Duration { get; } = duration;
}
