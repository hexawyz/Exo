namespace Exo.Monitors;

/// <summary>Provides information about a non continuous value, such as an input source.</summary>
/// <remarks>
/// While there is a standard for some non-continuous settings, it is not always exhaustive and various implementations can decide to extend it.
/// As such, we provide explicit information about non-continuous values, with the best of our knowledge.
/// </remarks>
public readonly struct NonContinuousValueDescription
{
	public NonContinuousValueDescription(ushort value, Guid nameIdString, string? customName)
	{
		Value = value;
		NameStringId = nameIdString;
		CustomName = customName;
	}

	/// <summary>Gets the value associated with this input source.</summary>
	public ushort Value { get; }
	/// <summary>Gets the name string ID for this value.</summary>
	/// <remarks>
	/// Non-continuous values that have been mapped by an explicit monitor description should generally provide this value.
	/// If possible, the name will be provided based on the values defined in the standard.
	/// If this ID is not provided, the value of the property will be <see cref="Guid.Empty"/>.
	/// </remarks>
	public Guid NameStringId { get; }
	/// <summary>Gets the custom name of the value.</summary>
	/// <remarks>
	/// Although rarely used, if used at all, monitors can provide custom name for non continuous values as part of their capabilities string.
	/// If this were to be the case, this property will contain the value contained in the capabilities string.
	/// </remarks>
	public string? CustomName { get; }
}
