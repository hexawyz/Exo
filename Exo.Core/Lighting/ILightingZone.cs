using Exo.Lighting.Effects;

namespace Exo.Lighting;

/// <summary>Represents a lighting zone in a lighting controller.</summary>
/// <remarks>
/// <para>
/// All implementations of lighting zone must also implement <see cref="ILightingZoneEffect{TEffect}"/> or <see cref="ILightingZoneAdvancedEffect{TEffect}"/> for each lighting effect supported by the
/// lighting zone. While this model is always not perfect, it allows for quickly testing lighting zones against well-known effects, and setting them relatively easily.
/// It does however require slightly more complex reflection code to determine a list of supported effects.
/// </para>
/// <para>
/// All lighting zone must support at least one effect.
/// Most lighting zones need to support at least <see cref="DisabledEffect"/>, but it is conceivable that some lighting zones could always be enabled.
/// </para>
/// </remarks>
public interface ILightingZone
{
	/// <summary>Gets a well-known unique identifier defining the lighting zone.</summary>
	/// <remarks>
	/// <para>
	/// Rather than augmenting the <see cref="ILightingZone"/> interface with many non-lighting related information, this property allows providing information externally, such as in a product database.
	/// It can then be used to provide information on the light zone such as physical dimensions, or number of adressable lights (which would also be accessible from <see cref="IAddressableLightZone"/>) and their layout.
	/// </para>
	/// <para>
	/// Light zone identifiers must be unique across a same device (i.e. no two light zones must have the same ID), and should generally be unique across different devices.
	/// Two instances of the same device should usually share the same zone IDs. (i.e. unless the devices have swappable parts or different revisions with different lighting zones)
	/// It is also possible to reuse a Zone ID for two devices of the same manufacturer having a lighting zone sharing exactly the same characteristics.
	/// </para>
	/// <para>
	/// The zone identifier will also be used by lighting components to store information about a specific light zone.
	/// In case of weird hardware configurations, we will provide well-known <see cref="Guid"/> values for generic light zone IDs (i.e. <c>LightZone0</c>, <c>LightZone1</c>) that can be used in case
	/// it is not possible to provide more specific well-known IDs.
	/// However, we expect the need for these to be quite rare.
	/// </para>
	/// </remarks>
	Guid ZoneId { get; }

	/// <summary>Gets the effect currently applied to the lighting zone.</summary>
	/// <remarks>
	/// <para>
	/// As many effects are represented using value types, this call may need to box the value to be returned.
	/// Whether or not boxing needs to happen depends on the implementation, as some implementations will already store the boxed value.
	/// </para>
	/// <para>
	/// This method may return <see cref="NotApplicableEffect"/> depending on the unified lighting status of the lighting controller.
	/// If unified lighting is enabled, all individual lighting zones must return <see cref="NotApplicableEffect"/>.
	/// If unified lighting is disabled, the unified lighting zone must return <see cref="NotApplicableEffect"/>.
	/// </para>
	/// </remarks>
	/// <returns></returns>
	ILightingEffect GetCurrentEffect();
}
