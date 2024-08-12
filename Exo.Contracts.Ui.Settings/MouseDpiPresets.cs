using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MouseDpiPresets
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public byte ActiveProfileIndex { get; init; }
	[DataMember(Order = 2)]
	public ImmutableArray<DotsPerInch> Profiles { get; init; }
}
