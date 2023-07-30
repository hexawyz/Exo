namespace Exo.Service;

public readonly struct DeviceWatchNotification
{
	public DeviceWatchNotification(WatchNotificationKind kind, DeviceInformation driverInformation, Driver? driver)
	{
		Kind = kind;
		DeviceInformation = driverInformation;
		Driver = driver;
	}

	/// <summary>Gets the kind of notification.</summary>
	public WatchNotificationKind Kind { get; }

	/// <summary>Gets the device information.</summary>
	/// <remarks>Because the driver instance may become invalid, useful information about the device and its driver is preserved here.</remarks>
	public DeviceInformation DeviceInformation { get; }

	/// <summary>Gets the actual driver instance.</summary>
	/// <remarks>
	/// This property is not assigned any value for a <see cref="WatchNotificationKind.Removal"/> notification.
	/// While the <see cref="Driver"/> instance may still be valid, and possibly somewhat useable, we consider it lost.
	/// The removal notification essentially serves for a way to notify consumers that they should remove their reference to the driver instance.
	/// </remarks>
	public Driver? Driver { get; }
}
