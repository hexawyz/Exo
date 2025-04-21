using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Exo.Service;

[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(TextMenuItem), "Default")]
[JsonDerivedType(typeof(SubMenuMenuItem), "SubMenu")]
[JsonDerivedType(typeof(SeparatorMenuItem), "Separator")]
public abstract class MenuItem
{
	public Guid ItemId { get; }

	[JsonIgnore]
	public abstract MenuItemType Type { get; }

	protected MenuItem(Guid itemId) => ItemId = itemId;

	public virtual bool NonRecursiveEquals(MenuItem other) => Equals(other);

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is MenuItem other && Equals(other);

	public virtual bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type;

	public override int GetHashCode() => HashCode.Combine(ItemId, Type);
}

public class TextMenuItem : MenuItem
{
	public string Text { get; }

	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.Default;

	public TextMenuItem(Guid itemId, string text) : base(itemId)
	{
		Text = text;
	}

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is TextMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is TextMenuItem otherItem && Equals(otherItem);

	public virtual bool Equals(TextMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type && Text == other.Text;

	public override int GetHashCode() => HashCode.Combine(ItemId, Type, Text);
}

public sealed class SubMenuMenuItem : TextMenuItem
{
	public ImmutableArray<MenuItem> MenuItems { get; }

	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.SubMenu;

	public SubMenuMenuItem(Guid itemId, string text, ImmutableArray<MenuItem> menuItems) : base(itemId, text)
	{
		MenuItems = menuItems;
	}

	public override bool NonRecursiveEquals(MenuItem other) => ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && ItemId == otherItem.ItemId && Type == otherItem.Type && Text == otherItem.Text;

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is SubMenuMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && Equals(otherItem);

	public override bool Equals(TextMenuItem other)
		=> ReferenceEquals(this, other) || other is SubMenuMenuItem otherItem && Equals(otherItem);

	public bool Equals(SubMenuMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type && Text == other.Text && MenuItems.AsSpan().SequenceEqual(other.MenuItems.AsSpan());

	public override int GetHashCode() => HashCode.Combine(ItemId, Type, Text, MenuItems.Length);
}

public sealed class SeparatorMenuItem : MenuItem
{
	[JsonIgnore]
	public override MenuItemType Type => MenuItemType.Separator;

	public SeparatorMenuItem(Guid itemId) : base(itemId)
	{
	}

	public override bool Equals(object? obj)
		=> ReferenceEquals(this, obj) || obj is SeparatorMenuItem other && Equals(other);

	public override bool Equals(MenuItem other)
		=> ReferenceEquals(this, other) || other is SeparatorMenuItem otherItem && Equals(otherItem);

	public bool Equals(SeparatorMenuItem other)
		=> ReferenceEquals(this, other) || ItemId == other.ItemId && Type == other.Type;

	public override int GetHashCode() => base.GetHashCode();
}
