namespace Exo.Rpc;

// Defines messages sent by the client to the server.
// ⚠️ Except for the NoOp and GitVersion commands, these messages are subject to changing at every release.
internal enum ExoUiProtocolClientMessage : byte
{
	/// <summary>Does nothing.</summary>
	/// <remarks>Can be used as a keep-alive mechanism, but we probably don't need one.</remarks>
	NoOp = 0,
	/// <summary>Communicates the client Git SHA1.</summary>
	/// <remarks>
	/// <para>Contents: 20 bytes (Git SHA1)</para>
	/// <para>
	/// Sent to the server in response to the server's version.
	/// If this version is not the same as the one from the server, the server will stop sending further messages.
	/// It is introduced to allow the debug-mode client to communicate with any server version by simply echoing the server version.
	/// </para>
	/// </remarks>
	GitVersion = 1,
	/// <summary>Updates the general service and Ui settings.</summary>
	UpdateSettings,
	/// <summary>Invokes a menu command.</summary>
	/// <remarks>Contents: 16 bytes (GUID)</remarks>
	InvokeMenuCommand,
	UpdateCustomMenu,
	/// <summary>Requests value updates for the specified sensor.</summary>
	/// <remarks>
	/// <para>
	/// Clients will provide a stream ID to be used for receiving updates.
	/// The stream ID is an arbitrary number managed entirely on the client side, such by atomically increasing a counter.
	/// The server will only prevent creating a stream with an ID that is currently in use. As such, it is perfectly valid for the client to reuse IDs.
	/// </para>
	/// <para>
	/// The client is presumed to be aware of the data format used by the requested sensor prior to emitting the request.
	/// No extra information will be sent as a result to a sensor request.
	/// For efficiency, the stream of sensor values will contain the strict minimum, which consists of the stream ID and the raw value for each update.
	/// </para>
	/// </remarks>
	SensorStart,
	SensorStop,
}

// Defines messages sent by the server to the client.
// ⚠️ Except for the NoOp and GitVersion commands, these messages are subject to changing at every release.
internal enum ExoUiProtocolServerMessage : byte
{
	/// <summary>Does nothing.</summary>
	/// <remarks>Can be used as a keep-alive mechanism, but we probably don't need one.</remarks>
	NoOp = 0,
	/// <summary>Communicates the server Git SHA1.</summary>
	/// <remarks>
	/// <para>Contents: 20 bytes (Git SHA1)</para>
	/// <para>
	/// First message to be sent after a connection is established.
	/// The client must respond to this with its own SHA1.
	/// Until the client has responded, and if the SHA1 matches, other messages will be sent.
	/// </para>
	/// </remarks>
	GitVersion = 1,
	/// <summary>Reports general service and UI settings.</summary>
	Settings,
	/// <summary>Reports system configuration errors, such as missing kernel driver.</summary>
	ConfigurationError,
	/// <summary>Initial enumeration of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemEnumeration,
	/// <summary>Addition of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemAdd ,
	/// <summary>Removal of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemRemove,
	/// <summary>Update of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemUpdate,
	DeviceEnumeration,
	DeviceAdd,
	DeviceRemove,
	DeviceUpdate,
	/// <summary>Acknowledges a sensor request.</summary>
	/// <remarks>
	/// For simplicity and avoiding any ambiguities, all sensor requests will receive an answer.
	/// If the result is <see cref="SensorStartStatus.Success"/>, it will be followed by a stream of <see cref="SensorValue"/> messages on the specified stream ID.
	/// Message values will be sent until <see cref="SensorStop"/> is emitted for the stream ID.
	/// </remarks>
	SensorStart,
	/// <summary>Communicates a sensor reading.</summary>
	/// <remarks>
	/// This message contains only the strict minimum necessary to provide sensor values to the client.
	/// The client is assumed to already have the necessary information about the sensor, which will indicate the type of data received in the message.
	/// </remarks>
	SensorValue,
	/// <summary>Communicates the end of readings for a sensor.</summary>
	/// <remarks>
	/// <para>
	/// This event is a strong guarantee that no further data will be received for the associated stream.
	/// It will be sent as a result of <see cref="ExoUiProtocolClientMessage.SensorStop"/> or of the sensor (device) becoming offline.
	/// </para>
	/// </remarks>
	SensorStop,
}

internal enum SensorStartStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
	SensorNotFound,
	StreamIdAlreadyInUse,
}
