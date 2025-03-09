namespace Exo.Rpc;

// Defines messages sent by the client to the server.
internal enum ExoHelperProtocolClientMessage : byte
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
	/// <summary>Invokes a menu command.</summary>
	/// <remarks>Contents: 16 bytes (GUID)</remarks>
	InvokeMenuCommand = 2,
	/// <summary>Response to an adapter request.</summary>
	/// <remarks></remarks>
	MonitorProxyAdapterResponse = 3,
	/// <summary>Response for acquiring control over a monitor.</summary>
	/// <remarks>
	/// <para>There is no response for the release, as it is always presumed successful.</para>
	/// </remarks>
	MonitorProxyMonitorAcquireResponse = 4,
	/// <summary>Response for monitor capabilities.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorCapabilitiesResponse = 5,
	/// <summary>Response for monitor VCP get.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorVcpGetResponse = 6,
	/// <summary>Response for monitor VCP set.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorVcpSetResponse = 7,
}

// Defines messages sent by the server to the client.
internal enum ExoHelperProtocolServerMessage : byte
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
	/// <summary>Overlay notification.</summary>
	/// <remarks></remarks>
	Overlay = 2,
	/// <summary>Initial enumeration of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemEnumeration = 3,
	/// <summary>Addition of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemAdd = 4,
	/// <summary>Removal of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemRemove = 5,
	/// <summary>Update of a custom menu item.</summary>
	/// <remarks></remarks>
	CustomMenuItemUpdate = 6,
	/// <summary>Request of an adapter.</summary>
	/// <remarks></remarks>
	MonitorProxyAdapterRequest = 7,
	/// <summary>Request for acquiring control over a monitor.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorAcquireRequest = 8,
	/// <summary>Request for releasing control over a monitor.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorReleaseRequest = 9,
	/// <summary>Request for monitor capabilities.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorCapabilitiesRequest = 10,
	/// <summary>Request for monitor VCP get.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorVcpGetRequest = 11,
	/// <summary>Request for monitor VCP set.</summary>
	/// <remarks></remarks>
	MonitorProxyMonitorVcpSetRequest = 12,
}
