namespace Exo;

public readonly record struct MouseProfileStatus
{
	/// <summary>Gets the current profile index if a profile is selected.</summary>
	public byte? ProfileIndex { get; init; }
}
