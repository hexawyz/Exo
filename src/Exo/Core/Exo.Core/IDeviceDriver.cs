namespace Exo;

public interface IDeviceDriver<TFeature>
	where TFeature : class, IDeviceFeature
{
	/// <summary>Gets a collection of device-related features supported by this instance of the driver.</summary>
	/// <remarks>It is expected that the feature collection may not be the same across different instances of the same driver.</remarks>
	IDeviceFeatureSet<TFeature> Features { get; }
}
