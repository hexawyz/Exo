namespace Exo.Features;

public readonly struct FeatureSetDescription
{
	public static FeatureSetDescription CreateDynamic<TFeature>(bool isAvailable)
		where TFeature : class, IDeviceFeature
	{
		// This is merely a way to validate the feature type.
		_ = FeatureSet.Empty<TFeature>();

		return new FeatureSetDescription(typeof(TFeature), true, isAvailable);
	}

	public static FeatureSetDescription CreateStatic<TFeature>()
		where TFeature : class, IDeviceFeature
	{
		// This is merely a way to validate the feature type.
		_ = FeatureSet.Empty<TFeature>();

		return new FeatureSetDescription(typeof(TFeature), false, true);
	}

	/// <summary>The base feature type for the feature set.</summary>
	public Type FeatureType { get; }

	/// <summary>Indicates that the feature set availability can change.</summary>
	public bool IsDynamic { get; }

	/// <summary>Indicates if the feature set is currently available for the device.</summary>
	public bool IsAvailable { get; }

	internal FeatureSetDescription(Type featureType, bool isDynamic, bool isAvailable)
	{
		FeatureType = featureType;
		IsDynamic = isDynamic;
		IsAvailable = isAvailable;
	}
}
