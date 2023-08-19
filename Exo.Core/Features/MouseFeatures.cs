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
	/// <summary>Gets the current DPI of the mouse.</summary>
	/// <remarks>
	/// <para>This value can change if the mouse supports different DPI settings.</para>
	/// <para>
	/// Some devices can use a different DPI setting for vertical and horizontal directions.
	/// Other devices should return the same value for both directions.
	/// </para>
	/// </remarks>
	DotsPerInch CurrentDpi { get; }
}

public interface IMouseDynamicDpiFeature : IMouseDpiFeature
{
	event Action<Driver, DotsPerInch> DpiChanged;
}
