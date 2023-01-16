using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Exo;

internal static class FeatureCollection
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
}

/// <summary>Quickly instanciate new feature collections.</summary>
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
}
