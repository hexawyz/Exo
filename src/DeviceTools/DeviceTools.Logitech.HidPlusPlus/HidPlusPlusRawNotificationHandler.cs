namespace DeviceTools.Logitech.HidPlusPlus;

/// <summary>Handles a raw notification message.</summary>
/// <remarks>
/// <para>
/// Notification handles will be called within the message processing loop.
/// As such, they should avoid throwing exceptions and keep their execution as short as possible.
/// Handlers should quickly discard irrelevant message and copy relevant notification messages into a queue for asynchronous processing.
/// </para>
/// <para>
/// While messages are pre-parsed to extract relevant information during the main processing loop,
/// passing raw messages to notification handles allows to abstract away the difference between various HID++ implementations.
/// It does, however, push the responsibility of correctly interpreting the messages to the code processing the notifications.
/// This should not be a problem most of the time, but it provides less guidance than the API exposed to send messages.
/// </para>
/// </remarks>
/// <param name="message">The raw message containing the notification.</param>
public delegate void HidPlusPlusRawNotificationHandler(ReadOnlySpan<byte> message);
