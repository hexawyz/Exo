using System.Diagnostics;
using System.Runtime.Serialization;

namespace Exo.Programming;

// NB: Protobuf inheritance must be manually configured in the serializer for fields present here to be inherited.
[DebuggerDisplay("{Name,nq} ({Id})")]
public abstract class NamedElement
{
	[DataMember(Order = 1)]
	public required Guid Id { get; init; }
	[DataMember(Order = 2)]
	public required string Name { get; init; }
	[DataMember(Order = 3)]
	public string? Comment { get; init; }
}
