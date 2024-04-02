using System.Collections;

namespace Exo;

/// <summary>A set of device-related features exposed by a particular device driver instance.</summary>
/// <remarks>
/// <para>
/// This is the base, non generic implementation of a feature set, used in cases when the feature type is not known in advance.
/// </para>
/// <para>
/// All feature sets <b>must</b> implement the generic <see cref="IDeviceFeatureSet{TFeature}"/> with the feature type matching <see cref="FeatureType"/>.
/// i.e. if <see cref="FeatureType"/> is <see cref="Features.IGenericDeviceFeature"/>,
/// then the feature set is always assumed to implement <see cref="IDeviceFeatureSet{TFeature}"/> with <see cref="Features.IGenericDeviceFeature"/> as the feature type.
/// </para>
/// </remarks>
public interface IDeviceFeatureSet : IEnumerable
{
	Type FeatureType { get; }
	bool IsEmpty { get; }
}

/// <summary>A set of device-related features exposed by a particular device driver instance.</summary>
/// <remarks>
/// <para>
/// A same driver implementation may cover multiple devices or use cases, and as such, may not expose the same feature set depending on the device or its conditions.
/// This interface allows to dynamically enumerate and query the features supported by a driver.
/// </para>
/// </remarks>
public interface IDeviceFeatureSet<TFeature> : IDeviceFeatureSet, IEnumerable<KeyValuePair<Type, TFeature>>, ICollection
	where TFeature : class, IDeviceFeature
{
	Type IDeviceFeatureSet.FeatureType => typeof(TFeature);
	TFeature? this[Type type] { get; }
	T? GetFeature<T>() where T : class, TFeature;
	bool HasFeature<T>() where T : class, TFeature => GetFeature<T>() is not null;

	bool ICollection.IsSynchronized => false;
	object ICollection.SyncRoot => this;

	void ICollection.CopyTo(System.Array array, int index)
	{
		if (array is not KeyValuePair<Type, TFeature>[] dest) throw new ArgumentException();
		if ((uint)index > (uint)array.Length) throw new ArgumentOutOfRangeException(nameof(index));
		if ((uint)Count > (uint)(array.Length - index)) throw new ArgumentException();

		foreach (var feature in this)
		{
			dest[index++] = feature;
		}
	}
}
