using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Ui.Contracts;

[DataContract]
public sealed class MenuDefinition
{
	[DataMember(Order = 1)]
	public ImmutableArray<MenuItemDefinition> MenuItems { get; init; } = [];
}
