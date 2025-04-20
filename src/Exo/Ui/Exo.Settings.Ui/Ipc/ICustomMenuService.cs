using System.Collections.Immutable;

namespace Exo.Service.Ipc;

internal interface ICustomMenuService
{
	Task InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken);
	Task UpdateMenuAsync(ImmutableArray<MenuItem> menuItems, CancellationToken cancellationToken);
}
