namespace Exo.Overlay;

internal sealed class OverlayTestViewModel
{
	public OverlayContentViewModel Content { get; } = new("\uEF31", "3… 2… 1…", 3, 10, 42);

	public bool IsVisible
	{
		get => true;
		set { }
	}
}
