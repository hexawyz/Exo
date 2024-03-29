using System.Runtime.Serialization;

namespace Exo.Contracts.Ui;

[DataContract]
public sealed class MenuChangeNotification
{
	[DataMember(Order = 1)]
	public required WatchNotificationKind Kind { get; init; }

	[DataMember(Order = 2)]
	public required Guid ParentItemId { get; init; }

	[DataMember(Order = 3)]
	public required int Position { get; init; }

	[DataMember(Order = 4)]
	public required Guid ItemId { get; init; }

	[DataMember(Order = 5)]
	public required MenuItemType ItemType { get; init; }

	[DataMember(Order = 6)]
	public string? Text { get; init; }
}
