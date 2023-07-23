namespace Exo;

/// <summary>The base class for a device driver.</summary>
/// <remarks>
/// <para>
/// In order to identify and work with features exposed by drivers, they should implement <see cref="IDeviceDriver{TFeature}"/> for each <see cref="IDeviceFeature"/> they expose.
/// In addition to this, the generic collection by <see cref="Features"/> must return an aggregate of all features supported by all the facets.
/// </para>
/// <para>
/// Drivers whose feature sets can change should also implement the interface <see cref="INotifyFeaturesChanged"/>.
/// As it concerns the feature set directly, this is not exposed as an <see cref="IDeviceFeature"/>, but it could be.
/// </para>
/// <para>
/// When applicable, drivers should also implement the <see cref="IManagedDeviceDriver{TDeviceManager}"/> interface to indicate that they are (can be) managed by the specified device manager.
/// Use of this interface is not mandatory, but it is expected that most device managers will use this interface to detect and instanciate device drivers for devices they can manage.
/// </para>
/// </remarks>
public abstract class Driver : IDeviceDriver<IDeviceFeature>, IAsyncDisposable
{
	/// <summary>Gets a friendly name for this driver instance.</summary>
	/// <remarks>
	/// <para>
	/// This property allows providing a name that is more explicit than the generic friendly name contained in the <see cref="DisplayName"/> attribute.
	/// Generally, drivers supporting more than one device (e.g. monitor drivers) would expose the friendly name of the device here.
	/// </para>
	/// </remarks>
	public string FriendlyName { get; }

	/// <summary>Gets the configuration key used to load the configuration.</summary>
	/// <returns>A string that can be used to look for the device configuration.</returns>
	public DeviceConfigurationKey ConfigurationKey { get; }

	/// <summary>Gets the category of the device managed by this driver.</summary>
	public abstract DeviceCategory DeviceCategory { get; }

	/// <summary>Gets the list of all features associated with this driver</summary>
	public abstract IDeviceFeatureCollection<IDeviceFeature> Features { get; }

	protected Driver(string friendlyName, DeviceConfigurationKey configurationKey)
	{
		FriendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
		configurationKey.Validate();
		ConfigurationKey = configurationKey;
	}

	public abstract ValueTask DisposeAsync();
}
