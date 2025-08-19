using System.Diagnostics.CodeAnalysis;
using Exo.ColorFormats;
using Exo.Lighting.Effects;
using Exo.Features.Lighting;

namespace Exo.Lighting;

/// <summary>Defines a lighting zone that is addressable.</summary>
/// <remarks>
/// <para>
/// All addressable lighting zones must support setting <see cref="AddressableEffect"/> as the current effect.
/// </para>
/// <para>
/// By itself, <see cref="AddressableEffect"/> does not allow controlling lighting.
/// It does, however, allows enabling addressable lighting on a lighting zone that could support other effects. (Including the <see cref="DisabledEffect" />)
/// </para>
/// </remarks>
public interface IAddressableLightingZone : ILightingZone
{
	/// <summary>Determines the number of individually addressable lights in this zone.</summary>
	/// <remarks>
	/// This does not give any information on the physical layout of such LEDs.
	/// Layout data must be obtained by other means, as zones could be user-adjustable.
	/// </remarks>
	int AddressableLightCount { get; }

	/// <summary>Gets a value indicating the capabilities of the zone.</summary>
	/// <remarks></remarks>
	AddressableLightingZoneCapabilities Capabilities { get; }

	/// <summary>Gets the type of color elements supported by the zone.</summary>
	/// <remarks>
	/// <para>Zones would typically support <see cref="RgbColor"/>, but other more advanced models such as RGBW could be used sometimes</para>
	/// <para>
	/// Please note that each color format needs to have explicit software support to be usable.
	/// Any custom color format will be ignored.
	/// </para>
	/// </remarks>
	Type ColorType { get; }
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
}

/// <summary>Defines a programmable addressable lighting zone.</summary>
/// <remarks>
/// A programmable lighting zone is programmed by sending a sequence of frames to be played by the controller.
/// Each frame in the sequence needs to provide color data for each LED of the zone, and a duration indicating for how long the frame must be visible.
/// </remarks>
public interface IProgrammableAddressableLightingZone : IAddressableLightingZone
{
	/// <summary>Indicates the maximum number of frames that can be handled by the controller.</summary>
	int MaximumFrameCount { get; }
}

/// <summary>Defines a programmable addressable lighting zone with the specified color type.</summary>
/// <typeparam name="TColor">The type of color items supported by the lighting zone.</typeparam>
public interface IProgrammableAddressableLightingZone<TColor> : IProgrammableAddressableLightingZone, IAddressableLightingZone<TColor>
	where TColor : unmanaged
{
	// Initially thought of having a more raw way of programming those zones, and we may reintroduce that in some form later.
	// However, I don't think there's any immediate drawback to have the zone just using the effect implementation directly.
	// This has the added advantage that the device can serialize the effect in its memory if it has the capabilities.
	// Drivers should be able to just use the "standard" effect serialization through EffectSerializer, so we won't need anything particular.
	//ValueTask SetFramesAsync(ReadOnlyMemory<LightingEffectFrame<TColor>> frames);

	/// <summary>Applies the effect to the lighting zone with the specified parameters.</summary>
	/// <remarks>
	/// <para>
	/// Unless the device has native support for some of those effects, drivers should not declare programmable effects through <see cref="ILightingZoneEffect{TEffect}"/>.
	/// Exo will always prefer the native effect API through <see cref="ILightingZoneEffect{TEffect}"/> to the programmable effect API, however both are supposed to be interchangeable.
	/// </para>
	/// <para>
	/// The current effect must be retrieved through <see cref="ILightingZone.GetCurrentEffect"/>.
	/// </para>
	/// </remarks>
	/// <param name="effect">The effect  to apply.</param>
	void ApplyEffect(IProgrammableLightingEffect<TColor> effect);
}

public interface IDynamicAddressableLightingZone : IAddressableLightingZone
{
	/// <summary>Indicate the minimum frequency at which updates should be pushed to the device, if necessary.</summary>
	/// <remarks>
	/// <para>
	/// Some devices will disable dynamic lighting if updates have not ben pushed for some amount of time.
	/// By indicating a refresh deadline, it allows the lighting engine to push colors regularly to the device, so that dynamic lighting is kept alive.
	/// </para>
	/// <para>The refresh will be done by calling <see cref="IDynamicAddressableLightingZone{TColor}.SetColorsAsync(ReadOnlySpan{TColor}, int, int)"/> by indicating no updated colors.</para>
	/// </remarks>
	ushort? MaximumRefreshInterval { get; }
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
public interface IDynamicAddressableLightingZone<TColor> : IDynamicAddressableLightingZone, IAddressableLightingZone<TColor>
	where TColor : unmanaged
{
	/// <summary>Sets the colors of the zone.</summary>
	/// <remarks>
	/// <para>
	/// Upon calling this method, the lighting zone is expected and assumed to be switched to <see cref="AddressableEffect"/>.
	/// This is mostly useful to convey to the software that the lighting zone is currently in addressable mode.
	/// (TBD if we want to add/require an explicit "switch to dynamic" feature, but that does nto seem necessary at the moment)
	/// </para>
	/// <para>
	/// While <see cref="ILightingDeferredChangesFeature.ApplyChangesAsync(bool)"/> will always be called after all lighting zones from a specific device have been updated,
	/// implementations are free to interpret each call to <see cref="SetColorsAsync(ReadOnlySpan{TColor}, int, int)"/> as an implicit request to apply changes to the current zone.
	/// This allows drivers to use the most efficient implementation to support dynamic lighting on the device they support.
	/// </para>
	/// <para>
	/// It is expected that on many devices, the most efficient thing to do will be to straight up push the new colors to the device when SetColorsAsync is called,
	/// especially as it avoid extra buffering of updates within the driver.
	/// However, if updates require a more expensive or specific setup, such as acquiring a global mutex, it might be more efficient for the driver to do push all updates at the same time.
	/// </para>
	/// </remarks>
	/// <param name="colors">The entire color buffer to assign to the zone.</param>
	/// <param name="changedRangeIndex">The index of the first updated color since last call.</param>
	/// <param name="changedRangeLength">The length of the updated color range.</param>
	/// <returns></returns>
	ValueTask SetColorsAsync(ReadOnlySpan<TColor> colors, int changedRangeIndex, int changedRangeLength);
}

[Flags]
public enum AddressableLightingZoneCapabilities : byte
{
	/// <summary>The lighting zone supports programmed addressable effects.</summary>
	/// <remarks>
	/// <para>The zone must implement <see cref="IProgrammableAddressableLightingZone{TColor}"/>.</para>
	/// <para>
	/// Programmed effects work by sending pre-rendered frames are to the ARGB controller, in a similar way than many software-run dynamic effects would work.
	/// In many scenarios, programmed effects will be a good choice, as they are lighter on the computer, due to the ARGB controller doing all the work.
	/// Programmed effects do, however, not support any truly dynamic change such as reacting to sound or video.
	/// </para>
	/// </remarks>
	Programmable = 0b00000001,
	/// <summary>The lighting zone supports fully dynamic addressable effects.</summary>
	/// <remarks>
	/// <para>The zone must implement <see cref="IDynamicAddressableLightingZone{TColor}"/>.</para>
	/// <para>
	/// Dynamic addressable effect are the standard and more direct form of ARGB effects.
	/// They allow full flexibility in terms of what is rendered. However, the low-level nature of these means
	/// that dynamic effects will always consume at least some share CPU resources in order to manage the updates on each effect frame.
	/// </para>
	/// </remarks>
	Dynamic = 0b00000010,
	/// <summary>Indicates that LED updates can be done on a subset of the LEDs.</summary>
	/// <remarks>
	/// <para>This is only applicable for zones that support dynamic effects.</para>
	/// <para>
	/// Hardware supporting partial updates allow for sending smaller update packets to the device rather than refreshing the whole buffer.
	/// This could, in some scenarios, lead to performance gains, especially for devices where buffer updates need to be split into multiple commands.
	/// </para>
	/// </remarks>
	AllowPartialUpdates = 0b10000000,
}
