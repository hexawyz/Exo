namespace Exo.Features.KeyboardFeatures;

public readonly record struct BacklightState
{
	/// <summary>The current backlight level.</summary>
	/// <remarks>The battery percentage can be obtained by <c><see cref="CurrentLevel"/> / <see cref="MaximumLevel"/></c></remarks>
	public byte CurrentLevel { get; init; }

	/// <summary>The maximum backlight level.</summary>
	/// <remarks>The battery percentage can be obtained by <c><see cref="CurrentLevel"/> / <see cref="MaximumLevel"/></c></remarks>
	public byte MaximumLevel { get; init; }
}
