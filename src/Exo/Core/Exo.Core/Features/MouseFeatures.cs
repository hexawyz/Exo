using System.Collections.Immutable;

namespace Exo.Features.Mouses;

/// <summary>Mouse devices can report their DPI here.</summary>
/// <remarks>
/// <para>
/// Mouses that do not support DPI adjustment should expose this feature if the DPI value is known.
/// Mouses that support dynamic DPI changes should also expose <see cref="IMouseDynamicDpiFeature"/>.
/// </para>
/// </remarks>
public interface IMouseDpiFeature : IMouseDeviceFeature
{
	/// <summary>Gets the current DPI status of the mouse.</summary>
	/// <remarks>
	/// <para>This value can change if the mouse supports different DPI settings.</para>
	/// </remarks>
	MouseDpiStatus CurrentDpi { get; }
}

public interface IMouseDynamicDpiFeature : IMouseDpiFeature
{
	/// <summary>Gets a value indicating the maximum allowed DPI values.</summary>
	/// <remarks>It is expected that for most devices, the maximum DPI value for X and Y will be the same.</remarks>
	DotsPerInch MaximumDpi { get; }

	/// <summary>Gets a value indicating whether the horizontal and vertical DPIs can be different.</summary>
	/// <remarks>
	/// If this value is <see langword="false"/>, all DPI values are assumed to have equal <see cref="DotsPerInch.Horizontal"/> and <see cref="DotsPerInch.Vertical"/> values.
	/// As such, <see cref="DotsPerInch.Vertical"/> should be ignored and <see cref="DotsPerInch.Horizontal"/> should be assumed to be the DPI value for both X and Y dimensions.
	/// </remarks>
	bool AllowsSeparateXYDpi { get; }

	/// <summary>Occurs when the current DPI has changed.</summary>
	event Action<Driver, MouseDpiStatus> DpiChanged;
}

/// <summary>This feature exposes DPI presets supported by a mouse.</summary>
public interface IMouseDpiPresetsFeature : IMouseDynamicDpiFeature
{
	/// <summary>Gets a value indicating if the current preset can be changed by using this feature.</summary>
	bool CanChangePreset => true;

	/// <summary>Gets the current DPI presets of the mouse.</summary>
	/// <remarks>
	/// <para>These presets can change if the mouse supports adjustable DPI presets.</para>
	/// <para>
	/// Some devices can use a different DPI setting for vertical and horizontal directions.
	/// Other devices should return the same value for both directions.
	/// </para>
	/// <para>
	/// The presets returned by this property will be referenced by <see cref="IMouseDpiFeature.CurrentDpi"/> and the <see cref="DpiChanged"/> event.
	/// </para>
	/// </remarks>
	ImmutableArray<DotsPerInch> DpiPresets { get; }

	/// <summary>Change the active mouse preset.</summary>
	/// <param name="activePresetIndex">The index of the preset that should be used.</param>
	/// <param name="cancellationToken"></param>
	ValueTask ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken);
}

/// <summary>This feature enables configuring the list of DPI presets of a mouse.</summary>
public interface IMouseConfigurableDpiPresetsFeature : IMouseDpiPresetsFeature
{
	/// <summary>Gets a value indicating the minimum number of DPI presets that can be defined.</summary>
	/// <remarks>This value cannot be less than <c>1</c>.</remarks>
	byte MinPresetCount => 1;

	/// <summary>Gets a value indicating the maximum number of DPI presets that can be defined.</summary>
	byte MaxPresetCount { get; }

	/// <summary>Sets the DPI presets of the mouse.</summary>
	/// <remarks>The list of presets passed must respect the constraints exposed by <see cref="AllowsSeparateXYDpi"/>, <see cref="MinPresetCount"/> and <see cref="MaxPresetCount"/>.</remarks>
	/// <param name="activePresetIndex">The index of preset to switch to, if supported.</param>
	/// <param name="dpiPresets">The list of DPI presets to use.</param>
	/// <param name="cancellationToken"></param>
	ValueTask SetDpiPresetsAsync(byte activePresetIndex, ImmutableArray<DotsPerInch> dpiPresets, CancellationToken cancellationToken);
}

/// <summary>This feature enables retrieving and configuring the polling rate used by a mouse device.</summary>
public interface IMouseConfigurablePollingFrequencyFeature : IMouseDeviceFeature
{
	/// <summary>Gets the current polling rate of the device.</summary>
	ushort PollingFrequency { get; }

	/// <summary>Gets a list of supported polling rates.</summary>
	ImmutableArray<ushort> SupportedPollingFrequencies { get; }

	/// <summary>Sets the polling rate of the device.</summary>
	/// <param name="pollingFrequency">The polling rate to use.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask SetPollingFrequencyAsync(ushort pollingFrequency, CancellationToken cancellationToken);
}

// NB: Mouse profiles is a complex feature to implement. This is far from complete, but it should allow basic operations.
// I'm not sure yet how to implement all of this, as the mouse profiles feature is likely to vary a lot between mouse models.
// The most important thing is the ability to receive a notification when a profile is changed, so that we can at least update the current DPI presets.
/// <summary>This feature enables working with mouses supporting multiple profiles.</summary>
public interface IMouseProfilesFeature : IMouseDeviceFeature
{
	event Action<Driver, MouseProfileStatus> ProfileChanged;

	byte ProfileCount { get; }

	byte? CurrentProfileIndex { get; }

	//Task ChangeCurrentProfileAsync(byte profileIndex, CancellationToken cancellationToken);
}
