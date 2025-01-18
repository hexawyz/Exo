namespace Exo.Service;

internal readonly struct ImageChangeNotification(WatchNotificationKind kind, ImageInformation imageInformation)
{
	public WatchNotificationKind Kind { get; } = kind;
	public ImageInformation ImageInformation { get; } = imageInformation;
}
