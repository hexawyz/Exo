namespace Exo.Core;

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
public abstract class Driver : IDeviceDriver<IDeviceFeature>
{
	/// <summary>Gets a friendly name for this driver instance.</summary>
	/// <remarks>
	/// <para>
	/// This property allows providing a name that is more explicit than the generic friendly name contained in the <see cref="DisplayName"/> attribute.
	/// Generally, drivers supporting more than one device (e.g. monitor drivers) would expose the friendly name of the device here.
	/// </para>
	/// </remarks>
	public abstract string FriendlyName { get; }

	protected Driver() { }

	/// <summary>Gets the configuration key used to load the configuration.</summary>
	/// <remarks>
	/// <para>
	/// The configuration key is used to uniquely identify the device in the configuration, and appropriately save and restore its settings.
	/// It will generally default to the device path, but a different key such as a serial number, can be used to identify a device even when connected to a different port.
	/// </para>
	/// <para>
	/// The returned key MUST be unique, deterministic, and no two driver instances must share the same key.
	/// Failure to comply to this rule will lead to various problems.
	/// </para>
	/// <para>
	/// As a default rule, all drivers should prefix their configuration key with a string uniquely identifying the driver in order to avoid name collisions.
	/// No need to be over-specific by having the exact driver type name, as it could change after a refactoring. A quick identifyiong key would be enough.
	/// </para>
	/// <para>
	/// TODO: We may need a mechanism to handle devices without a serial number than can frequently move ports but generally not have more than one instance.
	/// e.g. it is unusual to have two identical keyboards connected at the same time, but it should also work…
	/// It could also be desirable to allow settings to be ported across similar hardware configuration. e.g. Same monitor model, same keyboard model, but different devices because different office.
	/// Maybe in that case, having two key mechanisms could be helpful. e.g. a generic less-unique key and an globally unique key (SN# or device path)
	/// Maybe the non-unique key would be used preferably, but the unique key would be used to differentiate two instances. (e.g. "Keyboard Brand X #1" and "Keyboard Brand X #2")
	/// </para>
	/// </remarks>
	/// <returns>A string that can be used to look for the device configuration.</returns>
	protected abstract string GetConfigurationKey();

	/// <summary>Gets the list of all features associated with this driver</summary>
	public abstract IDeviceFeatureCollection<IDeviceFeature> Features { get; }
}
