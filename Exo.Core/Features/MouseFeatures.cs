namespace Exo.Features.MouseFeatures;

/// <summary>Mouse devices can report their DPI here.</summary>
/// <remarks>
/// <para>
/// Mouses that do not support DPI adjustment should expose this feature if the DPI value is known.
/// Mouses that support dynamic DPI changes should also expose <see cref="IMouseDynamicDpiFeature"/>.
/// </para>
/// </remarks>
public interface IMouseDpiFeature
{
	/// <summary>Gets the current DPI of the mouse.</summary>
	/// <remarks>This value can change if the mouse supports different DPI settings.</remarks>
	int CurrentDpi { get; }
}

public interface IMouseDynamicDpiFeature : IMouseDpiFeature
{
	event Action<Driver, int> DpiChanged;
}
