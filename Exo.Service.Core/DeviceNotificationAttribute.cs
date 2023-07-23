using System;

namespace Exo.Service;

public class DeviceNotificationAttribute : Attribute
{
	public Type DeviceNotificationEngine { get; }
}
