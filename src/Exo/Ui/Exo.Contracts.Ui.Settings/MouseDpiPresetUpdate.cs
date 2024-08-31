using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MouseDpiPresetUpdate
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required byte ActivePresetIndex { get; init; }
	[DataMember(Order = 3)]
	public required ImmutableArray<DotsPerInch> DpiPresets { get; init; }
}
