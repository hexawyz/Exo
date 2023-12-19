namespace Exo.Overlay;

public sealed class OverlayContentViewModel
{
	public OverlayContentViewModel(string? glyph, string? description)
		: this(glyph, description, 0, 0, null)
	{
	}
	public OverlayContentViewModel(string? glyph, string? description, long? value)
		: this(glyph, description, 0, 0, value)
	{
	}

	public OverlayContentViewModel(string? glyph, string? description, int currentLevel, int levelCount)
		: this(glyph, description, currentLevel, levelCount, null)
	{
	}

	public OverlayContentViewModel(string? glyph, string? description, int currentLevel, int levelCount, long? value)
	{
		Glyph = glyph;
		Description = description;
		CurrentLevel = currentLevel;
		LevelCount = levelCount;
		Value = value;
	}

	public string? Glyph { get; }

	public string? Description { get; }

	public int CurrentLevel { get; }

	public int LevelCount { get; }

	public long? Value { get; }

	public bool ShouldShowLevel => LevelCount > 0;

	public bool ShouldShowValue => Value is not null;
}
