namespace Exo.Overlay;

public sealed class OverlayContentViewModel
{
	public GlyphFont Font { get; init; } = GlyphFont.SegoeFluentIcons;

	public string? Glyph { get; init; }

	//public string? OverlayGlyph { get; init; }

	public string? Description { get; init; }

	public int CurrentLevel { get; init; }

	public int LevelCount { get; init; }

	public long? Value { get; init; }

	public bool ShouldShowLevel => LevelCount > 0;

	public bool ShouldShowValue => Value is not null;
}
