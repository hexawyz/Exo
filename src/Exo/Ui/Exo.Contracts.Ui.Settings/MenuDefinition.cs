using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MenuDefinition
{
	[DataMember(Order = 1)]
	public ImmutableArray<MenuItemDefinition> MenuItems { get; init; } = [];
}
