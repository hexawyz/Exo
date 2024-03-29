using System.Runtime.Serialization;

namespace Exo.Core.Contracts;

[DataContract]
public sealed class MenuItemReference
{
	[DataMember(Order = 1)]
	public Guid Id { get; init; }
}
