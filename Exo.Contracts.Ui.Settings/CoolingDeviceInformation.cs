using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class CoolingDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required ImmutableArray<CoolerInformation> Coolers { get; init; }
}
