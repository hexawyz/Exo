namespace Exo.Settings.Ui.Services;

public interface IMenuItemInvoker
{
	ValueTask InvokeMenuItemAsync(Guid menuItemId, CancellationToken cancellationToken);
}
