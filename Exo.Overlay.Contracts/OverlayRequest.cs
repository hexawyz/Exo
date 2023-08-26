using System.Runtime.Serialization;

namespace Exo.Overlay.Contracts;

[DataContract]
public sealed class OverlayRequest
{
	/// <summary>The kind of notification to display.</summary>
	/// <remarks>
	/// <para>
	/// The overlay service supports quite a few predefined notification types.
	/// It is recommended to use one of those when possible, rather than a custom one.
	/// </para>
	/// <para>
	/// Different notification kinds support different extra parameters.
	/// </para>
	/// </remarks>
	[DataMember(Order = 1)]
	public required OverlayNotificationKind NotificationKind { get; init; }

	/// <summary>When applicable, indicates the target or source device of the notification.</summary>
	/// <remarks>
	/// <para>
	/// This value must be provided when a notification is device-specific.
	/// It allows presenting the user with more useful information.
	/// </para>
	/// </remarks>
	[DataMember(Order = 2)]
	public string? DeviceName { get; init; }

	/// <summary>A complementary level value.</summary>
	/// <remarks>This will be used for some notifications such as battery notifications.</remarks>
	[DataMember(Order = 3)]
	public uint Level { get; init; }

	/// <summary>The maximum level.</summary>
	/// <remarks>This is used in conjunction with <see cref="Level"/>.</remarks>
	[DataMember(Order = 4)]
	public uint MaxLevel { get; init; }
}
