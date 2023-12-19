namespace Exo;

/// <summary>Represents the DPI status of a mouse.</summary>
public readonly record struct MouseDpiStatus
{
	/// <summary>Gets the current preset index, if applicable.</summary>
	/// <remarks>
	/// <para>For uniformity, and even if the mouse uses one-based preset indices, this value is zero-based.</para>
	/// <para>
	/// If the mouse does not support DPI presets, or if no preset is currently active, the property should return <see langword="null"/>.
	/// </para>
	/// </remarks>
	public byte? PresetIndex { get; init; }
	/// <summary>Gets the current DPI of the mouse.</summary>
	/// <remarks>
	/// <para>
	/// Some devices can use a different DPI setting for vertical and horizontal directions.
	/// Other devices should return the same value for both directions.
	/// </para>
	/// </remarks>
	public DotsPerInch Dpi { get; init; }
}
