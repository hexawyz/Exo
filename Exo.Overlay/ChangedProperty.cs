using System.ComponentModel;

namespace Exo.Overlay;

public static class ChangedProperty
{
	public static readonly PropertyChangedEventArgs Glyph = new(nameof(Glyph));
	public static readonly PropertyChangedEventArgs Description = new(nameof(Description));
	public static readonly PropertyChangedEventArgs IsVisible = new(nameof(IsVisible));
}
