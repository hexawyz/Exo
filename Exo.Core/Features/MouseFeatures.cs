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
	/// <summary>Occurs when the current DPI has changed.</summary>
	event Action<Driver, MouseDpiStatus> DpiChanged;
}

/// <summary>This feature exposes DPI presets supported by a mouse.</summary>
public interface IMouseDpiPresetFeature : IMouseDynamicDpiFeature
{
	///// <summary>Gets a value indicating if the current preset can be changed by using this feature.</summary>
	//bool CanChangePreset => true;

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

	///// <summary>Change the active mouse preset.</summary>
	///// <param name="activePresetIndex">The index of the preset that should be used.</param>
	///// <param name="cancellationToken"></param>
	//ValueTask ChangeCurrentPresetAsync(byte activePresetIndex, CancellationToken cancellationToken);
}

/// <summary>This feature enables configuring the list of DPI presets of a mouse.</summary>
public interface IMouseConfigurableDpiPresetsFeature : IMouseDpiPresetFeature
{
	/// <summary>Gets a value indicating whether the horizontal and vertical DPIs can be different.</summary>
	bool AllowsSeparateXYDpi { get; }

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
public interface IMouseConfigurablePollingRate : IMouseDeviceFeature
{
	/// <summary>Gets the current polling rate of the device.</summary>
	ushort PollingRate { get; }

	/// <summary>Gets a list of supported polling rates.</summary>
	ImmutableArray<ushort> SupportedPollingRates { get; }

	/// <summary>Sets the polling rate of the device.</summary>
	/// <param name="pollingRate">The polling rate to use.</param>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	ValueTask SetPollingRateAsync(ushort pollingRate, CancellationToken cancellationToken);
}
