namespace Exo.Service;

public readonly struct DeviceWatchNotification
{
	public DeviceWatchNotification(WatchNotificationKind kind, DeviceStateInformation deviceInformation, Driver? driver)
	{
		Kind = kind;
		DeviceInformation = deviceInformation;
		Driver = driver;
	}

	/// <summary>Gets the kind of notification.</summary>
	public WatchNotificationKind Kind { get; }

	/// <summary>Gets the device information.</summary>
	/// <remarks>Because the driver instance may become invalid, useful information about the device and its driver is preserved here.</remarks>
	public DeviceStateInformation DeviceInformation { get; }

	/// <summary>Gets the actual driver instance.</summary>
	/// <remarks>
	/// <para>
	/// Use of the driver instance for the <see cref="WatchNotificationKind.Removal"/> notification must be limited to cleanup operations such as unregistering handlers.
	/// While the <see cref="Driver"/> instance may still be valid, and possibly somewhat useable, we consider it lost.
	/// </para>
	/// <para>
	/// The driver instance is only provided for devices that are actually connected.
	/// Notifications <see cref="WatchNotificationKind.Addition"/> and <see cref="WatchNotificationKind.Removal"/> are guaranteed to provide the driver.
	/// For notifications other than <see cref="WatchNotificationKind.Removal"/>, <see cref="DeviceStateInformation.IsAvailable"/> will correlate with the presence of a driver instance.
	/// </para>
	/// </remarks>
	public Driver? Driver { get; }
}
