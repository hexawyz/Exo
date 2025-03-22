using System.Collections.Immutable;

namespace Exo.Contracts.Ui.Settings;

public readonly struct SensorDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required ImmutableArray<SensorInformation> Sensors { get; init; }
}
