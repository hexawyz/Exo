namespace Exo.Overlay;

internal sealed class OverlayTestViewModel
{
	public OverlayContentViewModel Content { get; } = new() { Glyph = "\uEF31", Description = "3… 2… 1…", CurrentLevel = 3, LevelCount = 10, Value = 42 };

	public bool IsVisible
	{
		get => true;
		set { }
	}
}
