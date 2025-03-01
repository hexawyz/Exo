using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class LightDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public LightDeviceCapabilities Capabilities { get; init; }
	[DataMember(Order = 3)]
	public required ImmutableArray<LightInformation> Lights { get; init; }
}
