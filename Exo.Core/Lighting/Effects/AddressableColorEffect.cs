namespace Exo.Lighting.Effects;

/// <summary>Represents an effect allowing lights of a zone to be addressed individually.</summary>
/// <remarks>
/// This effect itself only enables addressable lighting on the zone.
/// <see cref="IAddressableLightingZone{TColor}.SetColorsAsync(int, ReadOnlySpan{TColor})"/> must be used to update the individual lights.
/// </remarks>
[TypeId(0xBD4A9657, 0x5B39, 0x46AB, 0xB3, 0x2D, 0xA5, 0x24, 0xB7, 0xC0, 0xCB, 0x1C)]
public struct AddressableColorEffect : ISingletonLightingEffect
{
	/// <summary>Gets a boxed instance of the effect.</summary>
	public static ISingletonLightingEffect SharedInstance { get; } = new AddressableColorEffect();
}
