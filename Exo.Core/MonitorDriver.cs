using Exo.Features;

namespace Exo;

/// <summary>Base class for a monitor drivers.</summary>
/// <remarks>
/// <para>This base class fulfill no purposes in itself, other than to identify monitor drivers.</para>
/// <para>Most, if not all monitor have quite standard DDC/CI compliance, and as such, their drivers should derive from <see cref="DdcMonitorDriver"/> instead.</para>
/// </remarks>
public abstract class MonitorDriver : IDeviceDriver<IMonitorDeviceFeature>
{
	public abstract IDeviceFeatureCollection<IMonitorDeviceFeature> Features { get; }
}
