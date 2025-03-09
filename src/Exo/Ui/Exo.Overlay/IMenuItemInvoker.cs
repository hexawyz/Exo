namespace Exo.Overlay;

public interface IMenuItemInvoker
{
	ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken);
}
