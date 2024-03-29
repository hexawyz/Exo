using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class MenuItemReference
{
	[DataMember(Order = 1)]
	public Guid Id { get; init; }
}
