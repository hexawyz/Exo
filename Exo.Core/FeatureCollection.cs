using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Exo;

/// <summary>Quickly instantiate new feature collections.</summary>
/// <remarks>
/// <para>This class contains helpers to quickly create feature collections.</para>
/// <para>Usage of this class to create feature collections is not mandatory, but it can be helpful in simple use cases.</para>
/// <para>When possible, the feature collections will be more optimized than a naïve implementation.</para>
/// </remarks>
public static class FeatureCollection
{
	internal static void ValidateFeatureType(Type baseFeatureType, Type featureType)
	{
		if (featureType == baseFeatureType)
		{
			throw new InvalidOperationException($"A feature cannot have the type of the base feature type: {featureType}.");
		}
		ValidateInterfaceType(featureType);
	}

	internal static void ValidateInterfaceType(Type featureType)
	{
		if (!featureType.IsInterface)
		{
			throw new InvalidOperationException($"The specified type is not an interface: {featureType}.");
		}
	}

	internal static void ValidateDifferentFeatureTypes(Type type1, Type type2)
	{
		if (type1 == type2)
		{
			throw new InvalidOperationException($"The feature type {type1} has been specified twice.");
		}
	}

	/// <summary>Creates an empty feature collection.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <returns>A device collection that contains no feature.</returns>
	public static IDeviceFeatureCollection<TFeature> Empty<TFeature>()
		where TFeature : class, IDeviceFeature
		=> FeatureCollection<TFeature>.Empty();

	/// <summary>Creates a feature collection containing one feature implementation.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TSingleFeature">The type of the only feature.</typeparam>
	/// <param name="feature">The feature implementation.</param>
	/// <returns>A device collection that contains one feature.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TSingleFeature>(TSingleFeature feature)
		where TFeature : class, IDeviceFeature
		where TSingleFeature : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature);

	/// <summary>Creates a feature collection containing two feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <returns>A device collection that contains two features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2>(TFeature1 feature1, TFeature2 feature2)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2);

	/// <summary>Creates a feature collection containing three feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <returns>A device collection that contains three features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3)
	where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3);

	/// <summary>Creates a feature collection containing four feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <param name="feature4">The fourth feature implementation.</param>
	/// <returns>A device collection that contains four features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3, TFeature4>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3, feature4);

	/// <summary>Creates a feature collection containing five feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <param name="feature4">The fourth feature implementation.</param>
	/// <param name="feature5">The fifth feature implementation.</param>
	/// <returns>A device collection that contains five features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5
	)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3, feature4, feature5);
}

/// <summary>Quickly instantiate new feature collections.</summary>
/// <remarks>
/// <para>This class contains helpers to quickly create feature collections.</para>
/// <para>Usage of this class to create feature collections is not mandatory, but it can be helpful in simple use cases.</para>
/// <para>When possible, the feature collections will be more optimized than a naïve implementation.</para>
/// </remarks>
/// <typeparam name="TFeature"></typeparam>
internal static class FeatureCollection<TFeature>
	where TFeature : class, IDeviceFeature
{
	static FeatureCollection()
	{
		FeatureCollection.ValidateInterfaceType(typeof(TFeature));
	}

	public static IDeviceFeatureCollection<TFeature> Empty() => EmptyFeatureCollection.Instance;

	public static IDeviceFeatureCollection<TFeature> Create<TSingleFeature>(TSingleFeature feature)
		where TSingleFeature : class, TFeature
		=> new FixedFeatureCollection<TSingleFeature>(feature);

	public static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2>(TFeature1 feature1, TFeature2 feature2)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2>(feature1, feature2);

	public static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3>(feature1, feature2, feature3);

	public static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4>(feature1, feature2, feature3, feature4);

	public static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>(feature1, feature2, feature3, feature4, feature5);

	private sealed class EmptyFeatureCollection : IDeviceFeatureCollection<TFeature>
	{
		public static EmptyFeatureCollection Instance = new EmptyFeatureCollection();

		public TFeature? this[Type type] => null;

		public T? GetFeature<T>() where T : class, TFeature => null;

		private EmptyFeatureCollection() { }

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator() => Enumerable.Empty<KeyValuePair<Type, TFeature>>().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => Array.Empty<KeyValuePair<Type, TFeature>>().GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TSingleFeature> : IDeviceFeatureCollection<TFeature>
		where TSingleFeature : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TSingleFeature));
		}

		private readonly TSingleFeature _feature;

		public TFeature? this[Type type] => type == typeof(TSingleFeature) ? _feature : null;

		public T? GetFeature<T>() where T : class, TFeature => typeof(T) == typeof(TSingleFeature) ? Unsafe.As<T>(_feature) : null;

		internal FixedFeatureCollection(TSingleFeature feature) => _feature = feature;

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TSingleFeature), _feature);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (typeof(T) == typeof(TFeature1)) return Unsafe.As<T>(_feature1);
			else if (typeof(T) == typeof(TFeature2)) return Unsafe.As<T>(_feature2);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2)
		{
			_feature1 = feature1;
			_feature2 = feature2;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (typeof(T) == typeof(TFeature1)) return Unsafe.As<T>(_feature1);
			else if (typeof(T) == typeof(TFeature2)) return Unsafe.As<T>(_feature2);
			else if (typeof(T) == typeof(TFeature3)) return Unsafe.As<T>(_feature3);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature4));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;
		private readonly TFeature4 _feature4;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else if (type == typeof(TFeature4)) return _feature4;
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (typeof(T) == typeof(TFeature1)) return Unsafe.As<T>(_feature1);
			else if (typeof(T) == typeof(TFeature2)) return Unsafe.As<T>(_feature2);
			else if (typeof(T) == typeof(TFeature3)) return Unsafe.As<T>(_feature3);
			else if (typeof(T) == typeof(TFeature4)) return Unsafe.As<T>(_feature4);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
			_feature4 = feature4;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature4), _feature4);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature4));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature5));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;
		private readonly TFeature4 _feature4;
		private readonly TFeature5 _feature5;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else if (type == typeof(TFeature4)) return _feature4;
				else if (type == typeof(TFeature5)) return _feature5;
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (typeof(T) == typeof(TFeature1)) return Unsafe.As<T>(_feature1);
			else if (typeof(T) == typeof(TFeature2)) return Unsafe.As<T>(_feature2);
			else if (typeof(T) == typeof(TFeature3)) return Unsafe.As<T>(_feature3);
			else if (typeof(T) == typeof(TFeature4)) return Unsafe.As<T>(_feature4);
			else if (typeof(T) == typeof(TFeature5)) return Unsafe.As<T>(_feature5);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4, TFeature5 feature5)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
			_feature4 = feature4;
			_feature5 = feature5;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature4), _feature4);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature5), _feature5);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class MultiFeatureCollection : IDeviceFeatureCollection<TFeature>
	{
		private readonly HashSet<Type> _featureTypes;
		private readonly TFeature _implementation;

		public TFeature? this[Type type] => _featureTypes.Contains(type) ? _implementation : null;

		public T? GetFeature<T>()
			where T : class, TFeature
			=> _featureTypes.Contains(typeof(T)) ? Unsafe.As<T>(_implementation) : null;

		internal MultiFeatureCollection(HashSet<Type> featureTypes, TFeature implementation)
		{
			_featureTypes = featureTypes;
			_implementation = implementation;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			foreach (var featureType in _featureTypes)
			{
				yield return new KeyValuePair<Type, TFeature>(featureType, _implementation);
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
