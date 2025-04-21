namespace Exo.Service;

public sealed class MenuChangeNotification
{
	public required WatchNotificationKind Kind { get; init; }
	public required Guid ParentItemId { get; init; }
	public required uint Position { get; init; }
	public required Guid ItemId { get; init; }
	public required MenuItemType ItemType { get; init; }
	public string? Text { get; init; }
}
