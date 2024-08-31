using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MenuItemDefinition
{
	[DataMember(Order = 1)]
	public required Guid ItemId { get; init; }

	[DataMember(Order = 2)]
	public required MenuItemType Type { get; init; }

	[DataMember(Order = 3)]
	public string? Text { get; init; }

	[DataMember(Order = 4)]
	public ImmutableArray<MenuItemDefinition> MenuItems { get; init; } = [];
}
