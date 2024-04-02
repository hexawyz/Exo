using System.Diagnostics.CodeAnalysis;

namespace Exo.Service;

public readonly struct DeviceWatchNotification
{
	public DeviceWatchNotification(WatchNotificationKind kind, DeviceStateInformation deviceInformation, Driver? driver)
	{
		Kind = kind;
		DeviceInformation = deviceInformation;
		Driver = driver;
	}

	public DeviceWatchNotification(WatchNotificationKind kind, DeviceStateInformation deviceInformation, Driver? driver, IDeviceFeatureSet? featureSet)
	{
		Kind = kind;
		DeviceInformation = deviceInformation;
		Driver = driver;
		FeatureSet = featureSet;
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

	/// <summary>Gets the feature set associated with this notification.</summary>
	/// <remarks>
	/// <para>
	/// This will be sent for feature set update notifications and will indicate the kind of feature set that was updated.
	/// Additionally, in the case of device availability by feature type (i.e. <see cref="DeviceRegistry.WatchAvailableAsync{TFeature}(CancellationToken)"/>),
	/// this will be sent for all other notifications and will include the requested feature type.
	/// </para>
	/// <para>
	/// To provide coherence, notifications are sent with total order, and the state of the device at the time of a notification can always be assumed to be known from the previous notification(s).
	/// The consumer can decide whether each notification is of importance or not.
	/// Due to the asynchronous nature of the code, it is possible that, when the notification is received or processed, the features provided here are no longer valid.
	/// As in other situations, consumers of this notification must ensure that they properly catch possible exceptions that could result from the usage of these features.
	/// </para>
	/// <para>
	/// In the case of device availability by feature type, some feature update events will be treated as device arrivals or device removals,
	/// and all events occurring during the "offline" state will be filtered out.
	/// </para>
	/// </remarks>
	public IDeviceFeatureSet? FeatureSet { get; }
}
