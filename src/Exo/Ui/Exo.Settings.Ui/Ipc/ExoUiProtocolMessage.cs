namespace Exo.Settings.Ui.Ipc;

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
	ImageAddBegin,
	ImageAddCancel,
	ImageAddEnd,
	ImageRemove,
	LowPowerBatteryThreshold,
	IdleSleepTimer,
	WirelessBrightness,
	MouseActiveDpiPreset,
	MouseDpiPresets,
	MousePollingFrequency,
	/// <summary>Requests the update of lighting configuration for a device.</summary>
	LightingDeviceConfiguration,
	EmbeddedMonitorBuiltInGraphics,
	EmbeddedMonitorImage,
	/// <summary>Requests the update of a monitor setting.</summary>
	/// <remarks>Contents: Arbitrary request Id (varint32) + request.</remarks>
	MonitorSettingSet,
	/// <summary>Requests the refresh of all settings of a given monitor.</summary>
	/// <remarks>Contents: Arbitrary request Id (varint32) + request.</remarks>
	MonitorSettingRefresh,
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
	/// <summary>Sets a sensor as favorite.</summary>
	/// <remarks>This allows the service to remember the change.</remarks>
	SensorFavorite,
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
	/// <summary>Enumeration of initial metadata archives.</summary>
	/// <remarks>
	/// The first message of this kind on any connection will always be the default archives.
	/// Any of these messages will never be repeated.
	/// </remarks>
	MetadataSourcesEnumeration,
	/// <summary>Addition of metadata archives.</summary>
	MetadataSourcesAdd,
	/// <summary>Removal of metadata archives.</summary>
	MetadataSourcesRemove,
	/// <summary>Update of metadata archives. Specifically, signals the end of initialization.</summary>
	MetadataSourcesUpdate,
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
	ImageEnumeration,
	ImageAdd,
	ImageRemove,
	ImageUpdate,
	ImageAddOperationStatus,
	ImageRemoveOperationStatus,
	/// <summary>Provides information about a lighting effect.</summary>
	LightingEffect,
	DeviceEnumeration,
	DeviceAdd,
	DeviceRemove,
	DeviceUpdate,
	PowerDevice,
	BatteryState,
	LowPowerBatteryThreshold,
	IdleSleepTimer,
	WirelessBrightness,
	PowerDeviceOperationStatus,
	MouseDevice,
	MouseDpi,
	MouseDpiPresets,
	MousePollingFrequency,
	MouseDeviceOperationStatus,
	LightingDevice,
	LightingDeviceConfiguration,
	/// <summary>Acknowledges a lighting update.</summary>
	LightingDeviceOperationStatus,
	EmbeddedMonitorDevice,
	EmbeddedMonitorDeviceConfiguration,
	EmbeddedMonitorDeviceOperationStatus,
	/// <summary>Provides information about a monitor device.</summary>
	MonitorDevice,
	/// <summary>Provides updates on a monitor setting.</summary>
	MonitorSetting,
	/// <summary>Acknowledges a monitor setting update.</summary>
	MonitorSettingSetStatus,
	/// <summary>Acknowledges a monitor setting refresh.</summary>
	MonitorSettingRefreshStatus,
	/// <summary>Provides information about a sensor device.</summary>
	SensorDevice,
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
	/// <summary>Provides information about user-configuration of a sensor.</summary>
	/// <remarks>User configuration is purely cosmetic, only non-default configurations will be propagated.</remarks>
	SensorConfiguration,
}

internal enum ImageStorageOperationStatus : byte
{
	Success,
	Error,
	InvalidArgument,
	ImageNotFound,
	NameAlreadyInUse,
	ConcurrentOperation,
}

internal enum SensorStartStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
	SensorNotFound,
	StreamIdAlreadyInUse,
}

internal enum MonitorOperationStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
	SettingNotFound,
}

internal enum LightingDeviceOperationStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
	ZoneNotFound,
}

internal enum EmbeddedMonitorOperationStatus : byte
{
	Success,
	Error,
	InvalidArgument,
	DeviceNotFound,
	MonitorNotFound,
}

internal enum PowerDeviceOperationStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
}

internal enum MouseDeviceOperationStatus : byte
{
	Success,
	Error,
	DeviceNotFound,
}

[Flags]
internal enum LightingEffectFlags : byte
{
	None = 0b00000000,
	DefaultValue = 0b00000001,
	MinimumValue = 0b00000010,
	MaximumValue = 0b00000100,
	Enum = 0b00001000,
	Array = 0b00010000,
}

[Flags]
internal enum LightingDeviceConfigurationFlags : byte
{
	None = 0b00000000,
	IsUnified = 0b00000001,
	HasBrightness = 0b00000010,
	HasPalette = 0b00000100,
	Persist = 0b10000000,
}

[Flags]
internal enum LightingDeviceFlags : byte
{
	None = 0b00000000,
	HasUnifiedLighting = 0b00000001,
	HasBrightness = 0b00000010,
	HasPalette = 0b00000100,
	AlwaysPersisted = 0b01000000,
	CanPersist = 0b10000000,
}
