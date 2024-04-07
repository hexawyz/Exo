using Exo.Sensors;

namespace Exo.Features;

public interface ISensorsFeature : ISensorDeviceFeature
{
	IEnumerable<ISensor> Sensors { get; }
}

public interface IPolledSensorsFeature : ISensorDeviceFeature
{
}

public interface IStreamedSensorsFeature : ISensorDeviceFeature
{
}
