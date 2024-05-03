using Microsoft.Windows.ApplicationModel.Resources;

namespace Exo.Settings.Ui.ViewModels;

internal sealed class PageViewModel : IEquatable<PageViewModel?>
{
	private static readonly ResourceManager ResourceManager = new();

	private static string? TryGetResource(string key)
	{
		try
		{
			return ResourceManager.MainResourceMap.GetValue(key).ValueAsString;
		}
		catch
		{
			return null;
		}
	}

	public PageViewModel(string name, string icon) : this(name, null, icon, null) { }

	public PageViewModel(string name, string? displayName, string icon, object? parameter)
	{
		Name = name;
		_displayName = displayName;
		Icon = icon;
		Parameter = parameter;
	}

	public string Name { get; }
	private readonly string? _displayName;
	public string DisplayName => _displayName ?? TryGetResource($"Pages/{Name}") ?? "???";
	public string Icon { get; }
	public object? Parameter { get; }

	public override bool Equals(object? obj) => Equals(obj as PageViewModel);
	public bool Equals(PageViewModel? other) => other is not null && Name == other.Name && Icon == other.Icon && EqualityComparer<object?>.Default.Equals(Parameter, other.Parameter);
	public override int GetHashCode() => HashCode.Combine(Name, Icon, Parameter);

	public static bool operator ==(PageViewModel? left, PageViewModel? right) => EqualityComparer<PageViewModel>.Default.Equals(left, right);
	public static bool operator !=(PageViewModel? left, PageViewModel? right) => !(left == right);
}
