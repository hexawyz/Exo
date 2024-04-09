using System.Collections.Immutable;
using Exo.Sensors;

namespace Exo.Features;

public interface ISensorsFeature : ISensorDeviceFeature
{
	ImmutableArray<ISensor> Sensors { get; }
}
