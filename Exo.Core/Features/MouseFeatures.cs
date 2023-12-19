using System.Collections.Immutable;

namespace Exo.Features.MouseFeatures;

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

public interface IMouseDpiPresetFeature : IMouseDynamicDpiFeature
{
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
}
