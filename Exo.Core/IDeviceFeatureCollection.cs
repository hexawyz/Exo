using System;
using System.Collections.Generic;

namespace Exo
{
	/// <summary>A collection of device-related features exposed by a particular device driver instance.</summary>
	/// <remarks>
	/// <para>
	/// A same driver implementation may cover multiple devices or use cases, and as such, may not expose the same feature set depending on the device or its conditions.
	/// This interface allows to dynamically enumerate and query the features supported by a driver.
	/// </para>
	/// </remarks>
	public interface IDeviceFeatureCollection<TFeature> : IEnumerable<KeyValuePair<Type, TFeature>>
		where TFeature : class, IDeviceFeature
	{
		TFeature? this[Type type] { get; }
		T? GetFeature<T>() where T : class, TFeature;
		bool HasFeature<T>() where T : class, TFeature => GetFeature<T>() is not null;
	}
}
