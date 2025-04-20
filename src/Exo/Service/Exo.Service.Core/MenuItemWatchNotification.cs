namespace Exo.Service;

public readonly struct MenuItemWatchNotification
{
	public required WatchNotificationKind Kind { get; init; }
	public required Guid ParentItemId { get; init; }
	public required int Position { get; init; }
	public required MenuItem MenuItem { get; init; }
}
