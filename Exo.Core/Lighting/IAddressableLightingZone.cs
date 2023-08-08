using Exo.Lighting.Effects;

namespace Exo.Lighting;

/// <summary>Defines a lighting zone that is addressable.</summary>
/// <remarks>
/// <para>
/// All addressable lighting zones must support setting <see cref="AddressableColorEffect"/> as the current effect.
/// </para>
/// <para>
/// By itself, <see cref="AddressableColorEffect"/> does not allow controlling lighting.
/// It does, however, allows enabling addressable lighting on a lighting zone that could support other effects. (Including the <see cref="DisabledEffect" />)
/// </para>
/// </remarks>
public interface IAddressableLightingZone : ILightingZone, ILightingZoneEffect<AddressableColorEffect>
{
	/// <summary>Determines the number of individually addressable lights in this zone.</summary>
	/// <remarks>
	/// This does not give any information on the physical layout of such LEDs.
	/// Layout data must be obtained by other means, as zones could be user-adjustable.
	/// </remarks>
	int AddressableLightCount { get; }

	/// <summary>Gets a value indicating whether the zone supports partial updates</summary>
	bool AllowsRandomAccesses { get; }
}

/// <summary>Defines a lighting zone that is addressable with the specified color type.</summary>
/// <remarks>
/// <para>
/// For now, only <see cref="RgbColor"/> is supported as a color format.
/// More will be added in the future as needed.
/// </para>
/// <para>
/// It would technically be allowed for a lighting zone to support multiple color formats for compatibility,
/// and even exposing custom color formats if required. This support will be implemented at a later time if needed.
/// </para>
/// </remarks>
/// <typeparam name="TColor">The type of color items supported by the lighting zone.</typeparam>
public interface IAddressableLightingZone<TColor> : IAddressableLightingZone
	where TColor : unmanaged
{
	ValueTask SetColorsAsync(int index, ReadOnlySpan<TColor> colors);
}
