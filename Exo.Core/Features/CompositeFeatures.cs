namespace Exo.Features.CompositeFeatures;

/// <summary>Exposes the child device drivers of a composite device.</summary>
/// <remarks>The presence of this feature is mandatory for a composite driver.</remarks>
public interface ICompositeDeviceChildrenFeature : ICompositeDeviceFeature
{
	IReadOnlyList<Driver> Drivers { get; }
}

/// <summary>Exposes a notification for an update to the children of a composite driver.</summary>
/// <remarks>The presence of this feature is not mandatory, but indicates that the list of drivers returned by <see cref="ICompositeDeviceChildrenFeature"/> can change over time.</remarks>
public interface INotifyChildrenChange : ICompositeDeviceFeature
{
	event EventHandler ChildrenChanged;
}
