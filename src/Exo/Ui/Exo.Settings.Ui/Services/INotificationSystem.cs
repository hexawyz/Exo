namespace Exo.Settings.Ui.Services;

public interface INotificationSystem
{
	void PublishNotification(NotificationSeverity severity, string title, string message);
}
