using System.ComponentModel;

namespace Exo.Overlay;

public static class ChangedProperty
{
	public static readonly PropertyChangedEventArgs Content = new(nameof(Content));
	public static readonly PropertyChangedEventArgs IsVisible = new(nameof(IsVisible));
}
