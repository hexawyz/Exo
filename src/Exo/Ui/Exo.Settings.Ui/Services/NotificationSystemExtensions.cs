namespace Exo.Settings.Ui.Services;

public static class NotificationSystemExtensions
{
	public static void PublishError(this INotificationSystem notificationSystem, string title, string message)
		=> notificationSystem.PublishNotification(NotificationSeverity.Error, title, message);

	public static void PublishError(this INotificationSystem notificationSystem, Exception exception, string title)
		=> notificationSystem.PublishNotification(NotificationSeverity.Error, title, exception.Message);
}
