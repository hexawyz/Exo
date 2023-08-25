using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
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
	internal static void ValidateRootFeatureType(Type featureType)
	{
		ValidateInterfaceType(featureType);
		if (featureType.GetMembers(BindingFlags.Instance | BindingFlags.Public).Length != 0)
		{
			throw new InvalidOperationException($"Root feature types cannot have members: {featureType}.");
		}
	}

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

	private static readonly ConditionalWeakTable<Type, Func<Guid>> TypeIds = new();
	private static readonly MethodInfo GetGuidMethod =
		typeof(FeatureCollection)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Single(m => m.Name == nameof(GetGuid) && m.ContainsGenericParameters);

	internal static Guid GetNonCachedGuid(Type featureType)
		=> featureType.GetCustomAttribute<TypeIdAttribute>()?.Value ?? throw new InvalidOperationException($"Root feature types must have a unique identifier: {featureType}.");

	/// <summary>Gets the unique identified associated with the specified feature type.</summary>
	/// <typeparam name="TFeature">The type of the feature whose GUID should be retrieved.</typeparam>
	/// <returns>A unique ID that can uniquely identify the root feature type.</returns>
	public static Guid GetGuid(Type featureType)
		=> TypeIds.GetValue(featureType, t => GetGuidMethod.MakeGenericMethod(featureType).CreateDelegate<Func<Guid>>())();

	/// <summary>Gets the unique identified associated with the specified feature type.</summary>
	/// <typeparam name="TFeature">The type of the feature whose GUID should be retrieved.</typeparam>
	/// <returns>A unique ID that can uniquely identify the root feature type.</returns>
	public static Guid GetGuid<TFeature>()
		where TFeature : class, IDeviceFeature
		=> FeatureCollection<TFeature>.FeatureTypeGuid;

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

	/// <summary>Creates a feature collection containing six feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <param name="feature4">The fourth feature implementation.</param>
	/// <param name="feature5">The fifth feature implementation.</param>
	/// <param name="feature6">The sixth feature implementation.</param>
	/// <returns>A device collection that contains six features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6
	)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3, feature4, feature5, feature6);

	/// <summary>Creates a feature collection containing seven feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <typeparam name="TFeature7">The type of the seventh feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <param name="feature4">The fourth feature implementation.</param>
	/// <param name="feature5">The fifth feature implementation.</param>
	/// <param name="feature6">The sixth feature implementation.</param>
	/// <param name="feature7">The seventh feature implementation.</param>
	/// <param name="feature8">The eighth feature implementation.</param>
	/// <returns>A device collection that contains seven features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6,
		TFeature7 feature7
	)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3, feature4, feature5, feature6, feature7);

	/// <summary>Creates a feature collection containing eight feature implementations.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <typeparam name="TFeature7">The type of the seventh feature.</typeparam>
	/// <typeparam name="TFeature8">The type of the eighth feature.</typeparam>
	/// <param name="feature1">The first feature implementation.</param>
	/// <param name="feature2">The second feature implementation.</param>
	/// <param name="feature3">The third feature implementation.</param>
	/// <param name="feature4">The fourth feature implementation.</param>
	/// <param name="feature5">The fifth feature implementation.</param>
	/// <param name="feature6">The sixth feature implementation.</param>
	/// <param name="feature7">The seventh feature implementation.</param>
	/// <param name="feature8">The eighth feature implementation.</param>
	/// <returns>A device collection that contains eight features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6,
		TFeature7 feature7,
		TFeature8 feature8
	)
		where TFeature : class, IDeviceFeature
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		where TFeature8 : class, TFeature
		=> FeatureCollection<TFeature>.Create(feature1, feature2, feature3, feature4, feature5, feature6, feature7, feature8);

	/// <summary>Creates a feature collection for a type implementing one feature.</summary>
	/// <remarks>
	/// This method is only provided as syntactic sugar for a smooth transition from adding or removing a feature implementation in the code.
	/// The same effect can be achieved by relying on <see cref="Create{TFeature, TSingleFeature}(TSingleFeature)"/>.
	/// </remarks>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TSingleFeature">The type of the single feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains one feature.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TSingleFeature>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TSingleFeature
		where TSingleFeature : class, TFeature
		=> Create<TFeature, TSingleFeature>(implementation);

	/// <summary>Creates a feature collection for a type implementing two features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains two features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2>(implementation);

	/// <summary>Creates a feature collection for a type implementing three features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains three features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3>(implementation);

	/// <summary>Creates a feature collection for a type implementing four features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains four features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3, TFeature4>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4>(implementation);

	/// <summary>Creates a feature collection for a type implementing five features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains five features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>(implementation);

	/// <summary>Creates a feature collection for a type implementing six features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains six features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>(implementation);

	/// <summary>Creates a feature collection for a type implementing seven features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <typeparam name="TFeature7">The type of the seventh feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains seven features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>(implementation);

	/// <summary>Creates a feature collection for a type implementing eight features.</summary>
	/// <typeparam name="TFeature">The base feature type.</typeparam>
	/// <typeparam name="TImplementation">The feature implementation type.</typeparam>
	/// <typeparam name="TFeature1">The type of the first feature.</typeparam>
	/// <typeparam name="TFeature2">The type of the second feature.</typeparam>
	/// <typeparam name="TFeature3">The type of the third feature.</typeparam>
	/// <typeparam name="TFeature4">The type of the fourth feature.</typeparam>
	/// <typeparam name="TFeature5">The type of the fifth feature.</typeparam>
	/// <typeparam name="TFeature6">The type of the sixth feature.</typeparam>
	/// <typeparam name="TFeature7">The type of the seventh feature.</typeparam>
	/// <typeparam name="TFeature8">The type of the eighth feature.</typeparam>
	/// <param name="implementation">The implementation of all features.</param>
	/// <returns>A device collection that contains eight features.</returns>
	public static IDeviceFeatureCollection<TFeature> Create<TFeature, TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>(TImplementation implementation)
		where TFeature : class, IDeviceFeature
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		where TFeature8 : class, TFeature
		=> FeatureCollection<TFeature>.Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>(implementation);

	/// <summary>Creates a feature collection as a merge of two other collections.</summary>
	/// <remarks>For proper operation, the various feature collections must not overlap.</remarks>
	/// <typeparam name="TFeature1">The base feature type of the first collection.</typeparam>
	/// <param name="features1">The first feature collection.</param>
	/// <param name="fallbackFeatures">The base features to use as a fallback.</param>
	/// <returns>A device collection that exposes all features of the merged collections.</returns>
	public static IDeviceFeatureCollection<IDeviceFeature> CreateMerged<TFeature1>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<IDeviceFeature>? fallbackFeatures = null
	)
		where TFeature1 : class, IDeviceFeature
		=> FeatureCollection<IDeviceFeature>.CreateMerged(features1, fallbackFeatures);

	/// <summary>Creates a feature collection as a merge of three other collections.</summary>
	/// <remarks>For proper operation, the various feature collections must not overlap.</remarks>
	/// <typeparam name="TFeature1">The base feature type of the first collection.</typeparam>
	/// <typeparam name="TFeature2">The base feature type of the second collection.</typeparam>
	/// <param name="features1">The first feature collection.</param>
	/// <param name="features2">The second feature collection.</param>
	/// <param name="fallbackFeatures">The base features to use as a fallback.</param>
	/// <returns>A device collection that exposes all features of the merged collections.</returns>
	public static IDeviceFeatureCollection<IDeviceFeature> CreateMerged<TFeature1, TFeature2>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<IDeviceFeature>? fallbackFeatures = null
	)
		where TFeature1 : class, IDeviceFeature
		where TFeature2 : class, IDeviceFeature
		=> FeatureCollection<IDeviceFeature>.CreateMerged(features1, features2, fallbackFeatures);

	/// <summary>Creates a feature collection as a merge of four other collections.</summary>
	/// <remarks>For proper operation, the various feature collections must not overlap.</remarks>
	/// <typeparam name="TFeature1">The base feature type of the first collection.</typeparam>
	/// <typeparam name="TFeature2">The base feature type of the second collection.</typeparam>
	/// <typeparam name="TFeature3">The base feature type of the third collection.</typeparam>
	/// <param name="features1">The first feature collection.</param>
	/// <param name="features2">The second feature collection.</param>
	/// <param name="features3">The third feature collection.</param>
	/// <param name="fallbackFeatures">The base features to use as a fallback.</param>
	/// <returns>A device collection that exposes all features of the merged collections.</returns>
	public static IDeviceFeatureCollection<IDeviceFeature> CreateMerged<TFeature1, TFeature2, TFeature3>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<TFeature3> features3,
		IDeviceFeatureCollection<IDeviceFeature>? fallbackFeatures = null
	)
		where TFeature1 : class, IDeviceFeature
		where TFeature2 : class, IDeviceFeature
		where TFeature3 : class, IDeviceFeature
		=> FeatureCollection<IDeviceFeature>.CreateMerged(features1, features2, features3, fallbackFeatures);

	/// <summary>Creates a feature collection as a merge of five other collections.</summary>
	/// <remarks>For proper operation, the various feature collections must not overlap.</remarks>
	/// <typeparam name="TFeature1">The base feature type of the first collection.</typeparam>
	/// <typeparam name="TFeature2">The base feature type of the second collection.</typeparam>
	/// <typeparam name="TFeature3">The base feature type of the third collection.</typeparam>
	/// <typeparam name="TFeature3">The base feature type of the fourth collection.</typeparam>
	/// <param name="features1">The first feature collection.</param>
	/// <param name="features2">The second feature collection.</param>
	/// <param name="features3">The third feature collection.</param>
	/// <param name="features4">The fourth feature collection.</param>
	/// <param name="fallbackFeatures">The base features to use as a fallback.</param>
	/// <returns>A device collection that exposes all features of the merged collections.</returns>
	public static IDeviceFeatureCollection<IDeviceFeature> CreateMerged<TFeature1, TFeature2, TFeature3, TFeature4>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<TFeature3> features3,
		IDeviceFeatureCollection<TFeature4> features4,
		IDeviceFeatureCollection<IDeviceFeature>? fallbackFeatures = null
	)
		where TFeature1 : class, IDeviceFeature
		where TFeature2 : class, IDeviceFeature
		where TFeature3 : class, IDeviceFeature
		where TFeature4 : class, IDeviceFeature
		=> FeatureCollection<IDeviceFeature>.CreateMerged(features1, features2, features3, features4, fallbackFeatures);
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
	private static class Compatibility<TOtherFeature>
		where TOtherFeature : class, IDeviceFeature
	{
		public static readonly bool IsSubclass = typeof(TFeature).IsAssignableFrom(typeof(TOtherFeature));

		internal static readonly Func<IDeviceFeatureCollection<TFeature>, TOtherFeature?>? RelaxedGetFeature = CreateGetFeatureDelegate();

		private static Func<IDeviceFeatureCollection<TFeature>, TOtherFeature?>? CreateGetFeatureDelegate()
		{
			if (!IsSubclass) return null;

			var dynamicMethod = new DynamicMethod(nameof(GetFeature), typeof(TOtherFeature), new[] { typeof(IDeviceFeatureCollection<TFeature>) });
			var ilGenerator = dynamicMethod.GetILGenerator();

			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Callvirt, typeof(IDeviceFeatureCollection<TFeature>).GetMethod(nameof(GetFeature))!.MakeGenericMethod(typeof(TOtherFeature)));
			ilGenerator.Emit(OpCodes.Ret);

			return dynamicMethod.CreateDelegate<Func<IDeviceFeatureCollection<TFeature>, TOtherFeature?>>();
		}
	}

	public static TOtherFeature? GetFeature<TOtherFeature>(IDeviceFeatureCollection<TFeature> features)
		where TOtherFeature : class, IDeviceFeature
		=> Compatibility<TOtherFeature>.RelaxedGetFeature?.Invoke(features);

	internal static readonly Guid FeatureTypeGuid;

	static FeatureCollection()
	{
		FeatureCollection.ValidateInterfaceType(typeof(TFeature));
		FeatureTypeGuid = FeatureCollection.GetNonCachedGuid(typeof(TFeature));
	}

	internal static IDeviceFeatureCollection<TFeature> Empty() => EmptyFeatureCollection.Instance;

	private static IDeviceFeatureCollection<TFeature> Create(HashSet<Type> featureTypes, TFeature feature)
		=> new MultiFeatureCollection(featureTypes, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TSingleFeature>(TSingleFeature feature)
		where TSingleFeature : class, TFeature
		=> new FixedFeatureCollection<TSingleFeature>(feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2>(TFeature1 feature1, TFeature2 feature2)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2>(feature1, feature2);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3>(feature1, feature2, feature3);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4>(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4>(feature1, feature2, feature3, feature4);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>
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

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>(feature1, feature2, feature3, feature4, feature5, feature6);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6,
		TFeature7 feature7
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>(feature1, feature2, feature3, feature4, feature5, feature6, feature7);

	internal static IDeviceFeatureCollection<TFeature> Create<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>
	(
		TFeature1 feature1,
		TFeature2 feature2,
		TFeature3 feature3,
		TFeature4 feature4,
		TFeature5 feature5,
		TFeature6 feature6,
		TFeature7 feature7,
		TFeature8 feature8
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		where TFeature8 : class, TFeature
		=> new FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>(feature1, feature2, feature3, feature4, feature5, feature6, feature7, feature8);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3), typeof(TFeature4) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3), typeof(TFeature4), typeof(TFeature5) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3), typeof(TFeature4), typeof(TFeature5), typeof(TFeature6) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3), typeof(TFeature4), typeof(TFeature5), typeof(TFeature6), typeof(TFeature7) }, feature);

	internal static IDeviceFeatureCollection<TFeature> Create<TImplementation, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8>(TImplementation feature)
		where TImplementation : class, TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		where TFeature8 : class, TFeature
		=> Create(new() { typeof(TFeature1), typeof(TFeature2), typeof(TFeature3), typeof(TFeature4), typeof(TFeature5), typeof(TFeature6), typeof(TFeature7), typeof(TFeature8) }, feature);

	public static IDeviceFeatureCollection<TFeature> CreateMerged<TFeature1>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature>? fallbackFeatures
	)
		where TFeature1 : class, TFeature
		=> new MergedFeatureCollection<TFeature1>(features1, fallbackFeatures);

	public static IDeviceFeatureCollection<TFeature> CreateMerged<TFeature1, TFeature2>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<TFeature>? fallbackFeatures
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		=> new MergedFeatureCollection<TFeature1, TFeature2>(features1, features2, fallbackFeatures);

	public static IDeviceFeatureCollection<TFeature> CreateMerged<TFeature1, TFeature2, TFeature3>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<TFeature3> features3,
		IDeviceFeatureCollection<TFeature>? fallbackFeatures
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		=> new MergedFeatureCollection<TFeature1, TFeature2, TFeature3>(features1, features2, features3, fallbackFeatures);

	public static IDeviceFeatureCollection<TFeature> CreateMerged<TFeature1, TFeature2, TFeature3, TFeature4>
	(
		IDeviceFeatureCollection<TFeature1> features1,
		IDeviceFeatureCollection<TFeature2> features2,
		IDeviceFeatureCollection<TFeature3> features3,
		IDeviceFeatureCollection<TFeature4> features4,
		IDeviceFeatureCollection<TFeature>? fallbackFeatures
	)
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		=> new MergedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4>(features1, features2, features3, features4, fallbackFeatures);

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

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature4));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature5));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature6));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;
		private readonly TFeature4 _feature4;
		private readonly TFeature5 _feature5;
		private readonly TFeature6 _feature6;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else if (type == typeof(TFeature4)) return _feature4;
				else if (type == typeof(TFeature5)) return _feature5;
				else if (type == typeof(TFeature6)) return _feature6;
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
			else if (typeof(T) == typeof(TFeature6)) return Unsafe.As<T>(_feature6);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4, TFeature5 feature5, TFeature6 feature6)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
			_feature4 = feature4;
			_feature5 = feature5;
			_feature6 = feature6;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature4), _feature4);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature5), _feature5);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature6), _feature6);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature4));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature5));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature6));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature6), typeof(TFeature7));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;
		private readonly TFeature4 _feature4;
		private readonly TFeature5 _feature5;
		private readonly TFeature6 _feature6;
		private readonly TFeature7 _feature7;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else if (type == typeof(TFeature4)) return _feature4;
				else if (type == typeof(TFeature5)) return _feature5;
				else if (type == typeof(TFeature6)) return _feature6;
				else if (type == typeof(TFeature7)) return _feature7;
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
			else if (typeof(T) == typeof(TFeature6)) return Unsafe.As<T>(_feature6);
			else if (typeof(T) == typeof(TFeature7)) return Unsafe.As<T>(_feature7);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4, TFeature5 feature5, TFeature6 feature6, TFeature7 feature7)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
			_feature4 = feature4;
			_feature5 = feature5;
			_feature6 = feature6;
			_feature7 = feature7;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature4), _feature4);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature5), _feature5);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature6), _feature6);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature7), _feature7);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class FixedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4, TFeature5, TFeature6, TFeature7, TFeature8> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
		where TFeature5 : class, TFeature
		where TFeature6 : class, TFeature
		where TFeature7 : class, TFeature
		where TFeature8 : class, TFeature
	{
		static FixedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature4));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature5));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature6));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature7));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature4));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature3), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature5));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature4), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature6));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature5), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature6), typeof(TFeature7));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature6), typeof(TFeature8));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature7), typeof(TFeature8));
		}

		private readonly TFeature1 _feature1;
		private readonly TFeature2 _feature2;
		private readonly TFeature3 _feature3;
		private readonly TFeature4 _feature4;
		private readonly TFeature5 _feature5;
		private readonly TFeature6 _feature6;
		private readonly TFeature7 _feature7;
		private readonly TFeature8 _feature8;

		public TFeature? this[Type type]
		{
			get
			{
				if (type == typeof(TFeature1)) return _feature1;
				else if (type == typeof(TFeature2)) return _feature2;
				else if (type == typeof(TFeature3)) return _feature3;
				else if (type == typeof(TFeature4)) return _feature4;
				else if (type == typeof(TFeature5)) return _feature5;
				else if (type == typeof(TFeature6)) return _feature6;
				else if (type == typeof(TFeature7)) return _feature7;
				else if (type == typeof(TFeature8)) return _feature8;
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
			else if (typeof(T) == typeof(TFeature6)) return Unsafe.As<T>(_feature6);
			else if (typeof(T) == typeof(TFeature7)) return Unsafe.As<T>(_feature7);
			else if (typeof(T) == typeof(TFeature8)) return Unsafe.As<T>(_feature8);
			else return null;
		}

		internal FixedFeatureCollection(TFeature1 feature1, TFeature2 feature2, TFeature3 feature3, TFeature4 feature4, TFeature5 feature5, TFeature6 feature6, TFeature7 feature7, TFeature8 feature8)
		{
			_feature1 = feature1;
			_feature2 = feature2;
			_feature3 = feature3;
			_feature4 = feature4;
			_feature5 = feature5;
			_feature6 = feature6;
			_feature7 = feature7;
			_feature8 = feature8;
		}

		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature1), _feature1);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature2), _feature2);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature3), _feature3);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature4), _feature4);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature5), _feature5);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature6), _feature6);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature7), _feature7);
			yield return new KeyValuePair<Type, TFeature>(typeof(TFeature8), _feature8);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class MergedFeatureCollection<TFeature1> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
	{
		static MergedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
		}

		private readonly IDeviceFeatureCollection<TFeature1> _features1;
		private readonly IDeviceFeatureCollection<TFeature>? _fallbackFeatures;

		public MergedFeatureCollection(IDeviceFeatureCollection<TFeature1> features1, IDeviceFeatureCollection<TFeature>? fallbackFeatures)
		{
			_features1 = features1;
			_fallbackFeatures = fallbackFeatures;
		}

		public TFeature? this[Type type]
		{
			get
			{
				if (typeof(TFeature1).IsAssignableFrom(type)) return _features1[type];
				else if (_fallbackFeatures is not null) return _fallbackFeatures[type];
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (FeatureCollection<TFeature1>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature1>.GetFeature<T>(_features1);
			else if (FeatureCollection<TFeature>.Compatibility<T>.IsSubclass && _fallbackFeatures is not null) return FeatureCollection<TFeature>.GetFeature<T>(_fallbackFeatures);
			else return null;
		}


		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			foreach (var kvp in _features1)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature1>, KeyValuePair<Type, TFeature>>(kvp);
			}
			if (_fallbackFeatures != null)
			{
				foreach (var kvp in _fallbackFeatures)
				{
					yield return kvp;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class MergedFeatureCollection<TFeature1, TFeature2> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
	{
		static MergedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
		}

		private readonly IDeviceFeatureCollection<TFeature1> _features1;
		private readonly IDeviceFeatureCollection<TFeature2> _features2;
		private readonly IDeviceFeatureCollection<TFeature>? _fallbackFeatures;

		public MergedFeatureCollection(IDeviceFeatureCollection<TFeature1> features1, IDeviceFeatureCollection<TFeature2> features2, IDeviceFeatureCollection<TFeature>? fallbackFeatures)
		{
			_features1 = features1;
			_features2 = features2;
			_fallbackFeatures = fallbackFeatures;
		}

		public TFeature? this[Type type]
		{
			get
			{
				if (typeof(TFeature1).IsAssignableFrom(type)) return _features1[type];
				else if (typeof(TFeature2).IsAssignableFrom(type)) return _features2[type];
				else if (_fallbackFeatures is not null) return _fallbackFeatures[type];
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (FeatureCollection<TFeature1>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature1>.GetFeature<T>(_features1);
			else if (FeatureCollection<TFeature2>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature2>.GetFeature<T>(_features2);
			else if (FeatureCollection<TFeature>.Compatibility<T>.IsSubclass && _fallbackFeatures is not null) return FeatureCollection<TFeature>.GetFeature<T>(_fallbackFeatures);
			else return null;
		}


		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			foreach (var kvp in _features1)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature1>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features2)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature2>, KeyValuePair<Type, TFeature>>(kvp);
			}
			if (_fallbackFeatures != null)
			{
				foreach (var kvp in _fallbackFeatures)
				{
					yield return kvp;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class MergedFeatureCollection<TFeature1, TFeature2, TFeature3> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
	{
		static MergedFeatureCollection()
		{
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature1));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature2));
			FeatureCollection.ValidateFeatureType(typeof(TFeature), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature2));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature1), typeof(TFeature3));
			FeatureCollection.ValidateDifferentFeatureTypes(typeof(TFeature2), typeof(TFeature3));
		}

		private readonly IDeviceFeatureCollection<TFeature1> _features1;
		private readonly IDeviceFeatureCollection<TFeature2> _features2;
		private readonly IDeviceFeatureCollection<TFeature3> _features3;
		private readonly IDeviceFeatureCollection<TFeature>? _fallbackFeatures;

		public MergedFeatureCollection
		(
			IDeviceFeatureCollection<TFeature1> features1,
			IDeviceFeatureCollection<TFeature2> features2,
			IDeviceFeatureCollection<TFeature3> features3,
			IDeviceFeatureCollection<TFeature>? fallbackFeatures
		)
		{
			_features1 = features1;
			_features2 = features2;
			_features3 = features3;
			_fallbackFeatures = fallbackFeatures;
		}

		public TFeature? this[Type type]
		{
			get
			{
				if (typeof(TFeature1).IsAssignableFrom(type)) return _features1[type];
				else if (typeof(TFeature2).IsAssignableFrom(type)) return _features2[type];
				else if (typeof(TFeature3).IsAssignableFrom(type)) return _features3[type];
				else if (_fallbackFeatures is not null) return _fallbackFeatures[type];
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (FeatureCollection<TFeature1>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature1>.GetFeature<T>(_features1);
			else if (FeatureCollection<TFeature2>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature2>.GetFeature<T>(_features2);
			else if (FeatureCollection<TFeature3>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature3>.GetFeature<T>(_features3);
			else if (FeatureCollection<TFeature>.Compatibility<T>.IsSubclass && _fallbackFeatures is not null) return FeatureCollection<TFeature>.GetFeature<T>(_fallbackFeatures);
			else return null;
		}


		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			foreach (var kvp in _features1)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature1>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features2)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature2>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features3)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature3>, KeyValuePair<Type, TFeature>>(kvp);
			}
			if (_fallbackFeatures != null)
			{
				foreach (var kvp in _fallbackFeatures)
				{
					yield return kvp;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private sealed class MergedFeatureCollection<TFeature1, TFeature2, TFeature3, TFeature4> : IDeviceFeatureCollection<TFeature>
		where TFeature1 : class, TFeature
		where TFeature2 : class, TFeature
		where TFeature3 : class, TFeature
		where TFeature4 : class, TFeature
	{
		static MergedFeatureCollection()
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

		private readonly IDeviceFeatureCollection<TFeature1> _features1;
		private readonly IDeviceFeatureCollection<TFeature2> _features2;
		private readonly IDeviceFeatureCollection<TFeature3> _features3;
		private readonly IDeviceFeatureCollection<TFeature4> _features4;
		private readonly IDeviceFeatureCollection<TFeature>? _fallbackFeatures;

		public MergedFeatureCollection
		(
			IDeviceFeatureCollection<TFeature1> features1,
			IDeviceFeatureCollection<TFeature2> features2,
			IDeviceFeatureCollection<TFeature3> features3,
			IDeviceFeatureCollection<TFeature4> features4,
			IDeviceFeatureCollection<TFeature>? fallbackFeatures
		)
		{
			_features1 = features1;
			_features2 = features2;
			_features3 = features3;
			_features4 = features4;
			_fallbackFeatures = fallbackFeatures;
		}

		public TFeature? this[Type type]
		{
			get
			{
				if (typeof(TFeature1).IsAssignableFrom(type)) return _features1[type];
				else if (typeof(TFeature2).IsAssignableFrom(type)) return _features2[type];
				else if (typeof(TFeature3).IsAssignableFrom(type)) return _features3[type];
				else if (typeof(TFeature4).IsAssignableFrom(type)) return _features4[type];
				else if (_fallbackFeatures is not null) return _fallbackFeatures[type];
				else return null;
			}
		}

		public T? GetFeature<T>()
			where T : class, TFeature
		{
			if (FeatureCollection<TFeature1>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature1>.GetFeature<T>(_features1);
			else if (FeatureCollection<TFeature2>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature2>.GetFeature<T>(_features2);
			else if (FeatureCollection<TFeature3>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature3>.GetFeature<T>(_features3);
			else if (FeatureCollection<TFeature4>.Compatibility<T>.IsSubclass) return FeatureCollection<TFeature4>.GetFeature<T>(_features4);
			else if (FeatureCollection<TFeature>.Compatibility<T>.IsSubclass && _fallbackFeatures is not null) return FeatureCollection<TFeature>.GetFeature<T>(_fallbackFeatures);
			else return null;
		}


		public IEnumerator<KeyValuePair<Type, TFeature>> GetEnumerator()
		{
			foreach (var kvp in _features1)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature1>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features2)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature2>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features3)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature3>, KeyValuePair<Type, TFeature>>(kvp);
			}
			foreach (var kvp in _features4)
			{
				yield return Unsafe.BitCast<KeyValuePair<Type, TFeature4>, KeyValuePair<Type, TFeature>>(kvp);
			}
			if (_fallbackFeatures != null)
			{
				foreach (var kvp in _fallbackFeatures)
				{
					yield return kvp;
				}
			}
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
