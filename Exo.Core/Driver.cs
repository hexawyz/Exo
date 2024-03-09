using Exo.Features;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
/// Use of this interface is not mandatory, but it is expected that most device managers will use this interface to detect and instantiate device drivers for devices they can manage.
/// </para>
/// </remarks>
public abstract class Driver : Component
{
	// Implement a per-type cache of features for drivers providing simple non-dynamic feature sets.
	private static readonly ConditionalWeakTable<Type, FeatureSetDescription[]> DriverFeatureCache = new();

	private static ImmutableArray<FeatureSetDescription> GetDefinedDriverFeatures(Type driverType)
		=> ImmutableCollectionsMarshal.AsImmutableArray(DriverFeatureCache.GetValue(driverType, GetNonCachedDefinedDriverFeatures));

	private static FeatureSetDescription[] GetNonCachedDefinedDriverFeatures(Type driverType)
	{
		var featureTypes = new List<FeatureSetDescription>();
		foreach (var interfaceType in driverType.GetInterfaces())
		{
			var t = interfaceType;
			while (t.BaseType is not null)
			{
				t = t.BaseType;
			}

			if (t.IsGenericType && t != typeof(IDeviceDriver<IDeviceFeature>) && t.GetGenericTypeDefinition() == typeof(IDeviceDriver<>))
			{
				featureTypes.Add(new(t.GetGenericArguments()[0], false, true));
			}
		}

		return [.. featureTypes];
	}

	/// <summary>Gets a friendly name for this driver instance.</summary>
	/// <remarks>
	/// <para>
	/// This property allows providing a name that is more explicit than the generic friendly name contained in the <see cref="DisplayName"/> attribute.
	/// Generally, drivers supporting more than one device (e.g. monitor drivers) would expose the friendly name of the device here.
	/// </para>
	/// </remarks>
	public override string FriendlyName { get; }

	/// <summary>Gets the configuration key used to load the configuration.</summary>
	/// <returns>A string that can be used to look for the device configuration.</returns>
	public DeviceConfigurationKey ConfigurationKey { get; }

	/// <summary>Gets the category of the device managed by this driver.</summary>
	public abstract DeviceCategory DeviceCategory { get; }

	/// <summary>Gets the description of all the supported feature sets.</summary>
	/// <remarks>
	/// <para>
	/// This must always return a snapshot of all the feature sets supported by the current device, along with their current availability.
	/// Devices for which feature sets can become dynamically online or offline must provide the <see cref="IVariableFeatureSetDeviceFeature"/> feature to notify of changes.
	/// </para>
	/// <para>
	/// The value returned here can change between calls, as feature sets become available or unavailable.
	/// <see cref="IVariableFeatureSetDeviceFeature.FeatureAvailabilityChanged"/> will be used to notify of a change in availability for a given feature set.
	/// </para>
	/// <para>A feature set must always be reported as unavailable when the corresponding collection is empty.</para>
	/// </remarks>
	public virtual ImmutableArray<FeatureSetDescription> FeatureSets => GetDefinedDriverFeatures(GetType());

	protected Driver(string friendlyName, DeviceConfigurationKey configurationKey)
	{
		FriendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
		ConfigurationKey = configurationKey;
	}

	/// <summary>Gets the feature collection for the specified base feature type.</summary>
	/// <remarks>
	/// <para>
	/// Implementors should return <see cref="FeatureCollection.Empty{TFeature}"/> for unavailable features anf for all unsupported feature sets.
	/// In the case of supported feature sets, an empty collection is always assumed to indicate the feature set being unavailable.
	/// </para>
	/// <para>
	/// Returning an empty feature set in all supported cases makes the work of the callers easier, not necessitating to check if a given feature set is supported before requesting it.
	/// It also does not incur any excessive work in the implementors, as the fallback to <see cref="FeatureCollection.Empty{TFeature}"/> is very easy to implement.
	/// </para>
	/// </remarks>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <returns>A feature collection of the specified type.</returns>
	public virtual IDeviceFeatureCollection<TFeature> GetFeatures<TFeature>()
		where TFeature : class, IDeviceFeature
		=> (this as IDeviceDriver<TFeature>)?.Features ?? FeatureCollection.Empty<TFeature>();
}
