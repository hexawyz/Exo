namespace Exo.Service;

// TODO: Refactor this (away?)
public readonly struct LightingDeviceWatchNotification
{
	public LightingDeviceWatchNotification(WatchNotificationKind kind, DeviceStateInformation deviceInformation, LightingDeviceInformation lightingDeviceInformation, Driver driver)
	{
		Kind = kind;
		DeviceInformation = deviceInformation;
		LightingDeviceInformation = lightingDeviceInformation;
		Driver = driver;
	}

	/// <summary>Gets the kind of notification.</summary>
	public WatchNotificationKind Kind { get; }

	// TODO: Remove ? This should be correlated with the main device notifications. (Especially because devices will now be persisted after disconnection)
	/// <summary>Gets the device information.</summary>
	/// <remarks>Because the driver instance may become invalid, useful information about the device and its driver is preserved here.</remarks>
	public DeviceStateInformation DeviceInformation { get; }

	/// <summary>Gets the lighting information.</summary>
	public LightingDeviceInformation LightingDeviceInformation { get; }

	/// <summary>Gets the actual driver instance.</summary>
	/// <remarks>
	/// This property is not assigned any value for a <see cref="WatchNotificationKind.Removal"/> notification.
	/// While the <see cref="Driver"/> instance may still be valid, and possibly somewhat useable, we consider it lost.
	/// The removal notification essentially serves for a way to notify consumers that they should remove their reference to the driver instance.
	/// </remarks>
	public Driver Driver { get; }
}
