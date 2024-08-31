namespace Exo.Service;

public class DeviceNotificationAttribute : Attribute
{
	public Type DeviceNotificationEngine { get; }

	public DeviceNotificationAttribute(Type deviceNotificationEngine) => DeviceNotificationEngine = deviceNotificationEngine;
}
