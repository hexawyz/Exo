namespace Exo.Overlay;

public sealed class OverlayContentViewModel
{
	public OverlayContentViewModel(string? glyph, string? description)
		: this(glyph, description, 0, 0)
	{
	}

	public OverlayContentViewModel(string? glyph, string? description, int currentLevel, int levelCount)
	{
		Glyph = glyph;
		Description = description;
		CurrentLevel = currentLevel;
		LevelCount = levelCount;
	}

	public string? Glyph { get; }

	public string? Description { get; }

	public int CurrentLevel { get; }

	public int LevelCount { get; }
}
