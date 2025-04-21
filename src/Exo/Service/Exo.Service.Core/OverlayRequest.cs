namespace Exo.Service;

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
	public required OverlayNotificationKind NotificationKind { get; init; }

	/// <summary>When applicable, indicates the target or source device of the notification.</summary>
	/// <remarks>
	/// <para>
	/// This value must be provided when a notification is device-specific.
	/// It allows presenting the user with more useful information.
	/// </para>
	/// </remarks>
	public string? DeviceName { get; init; }

	/// <summary>A complementary level value.</summary>
	/// <remarks>This will be used for some notifications such as battery notifications.</remarks>
	public uint Level { get; init; }

	/// <summary>The maximum level.</summary>
	/// <remarks>This is used in conjunction with <see cref="Level"/>.</remarks>
	public uint MaxLevel { get; init; }

	/// <summary>A numeric value associated with the notification.</summary>
	/// <remarks>
	/// This can be used for values that cannot fit on a level scale.
	/// It can still be used as a complement to <see cref="Level"/> and <see cref="MaxLevel"/>.
	/// For example, providing the exact mouse DPI value while also providing the predefined DPI level index.
	/// </remarks>
	public long Value { get; init; }
}
