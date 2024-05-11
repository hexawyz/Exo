namespace Exo.Lighting.Effects;

/// <summary>Effects that expose a brightness setting.</summary>
/// <remarks>
/// <para>
/// Some devices support setting a brightness level as part of lighting effects, per-lighting zone.
/// Such effects that expose a brightness setting must implement this interface to notify that they will override the device's default brightness setting.
/// </para>
/// <para>
/// Many devices will actually persist the global brightness. Changing it often could possibly lead to premature wear.
/// Drivers should not seek to expose the brightness setting on effects if it does not make sense on a hardware/firmware level.
/// Only devices that support a per-effect dynamic brightness setting should expose this feature.
/// </para>
/// <para>
/// Devices that support a per-effect brightness setting should provide the <see cref="Exo.Features.Lighting.ILightingBrightnessFeature" /> feature and use this brightness value as a default
/// value for all standard effects that are assigned without a brightness. (e.g. <see cref="StaticColorEffect"/>)
/// Obviously, the brightness expressed in <see cref="BrightnessLevel"/> would be expressed in the same scale as the global brightness.
/// </para>
/// </remarks>
public interface IBrightnessLightingEffect : ILightingEffect
{
	byte BrightnessLevel { get; }
}
