using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Exo.Contracts.Ui.Overlay;

/// <summary>Service contract used to proxy monitor control requests between the service and user mode.</summary>
/// <remarks>
/// This is used as a fallback in case drivers for adapters are not available or not working.
/// This is especially a problem for Intel devices which either don't provide any way to access the driver features or for which the control library is provided but not working.
/// </remarks>
[ServiceContract(Name = "MonitorControlProxy")]
public interface IMonitorControlProxyService
{
	[OperationContract(Name = "ProcessAdapterRequests")]
	IAsyncEnumerable<AdapterRequest> ProcessAdapterRequestsAsync(Guid sessionId, IAsyncEnumerable<AdapterResponse> responses, CancellationToken cancellationToken);

	[OperationContract(Name = "ProcessMonitorRequests")]
	IAsyncEnumerable<MonitorRequest> ProcessMonitorRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorResponse> responses, CancellationToken cancellationToken);

	[OperationContract(Name = "ProcessCapabilitiesRequests")]
	IAsyncEnumerable<MonitorCapabilitiesRequest> ProcessCapabilitiesRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorCapabilitiesResponse> responses, CancellationToken cancellationToken);

	[OperationContract(Name = "ProcessVcpGetRequests")]
	IAsyncEnumerable<MonitorVcpGetRequest> ProcessVcpGetRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorVcpGetResponse> responses, CancellationToken cancellationToken);

	[OperationContract(Name = "ProcessVcpSetRequests")]
	IAsyncEnumerable<MonitorVcpSetRequest> ProcessVcpSetRequestsAsync(Guid sessionId, IAsyncEnumerable<MonitorVcpSetResponse> responses, CancellationToken cancellationToken);

	[OperationContract(Name = "ProcessVcpSetRequests")]
	IAsyncEnumerable<MonitorReleaseRequest> EnumerateMonitorsToReleaseAsync(Guid sessionId, CancellationToken cancellationToken);
}

public enum ResponseStatus : byte
{
	Success = 0,
	NotFound = 1,
	Error = 2,
}

public interface IRequestId
{
	int RequestId { get; }
}

[DataContract]
public sealed class AdapterRequest : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required string DeviceName { get; init; }
}

[DataContract]
public sealed class AdapterResponse : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ResponseStatus Status { get; init; }
	[DataMember(Order = 3)]
	public required ulong AdapterId { get; init; }
}

[DataContract]
public sealed class MonitorRequest : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ulong AdapterId { get; init; }
	[DataMember(Order = 3)]
	public required ushort EdidVendorId { get; init; }
	[DataMember(Order = 4)]
	public required ushort EdidProductId { get; init; }
	[DataMember(Order = 5)]
	public required uint IdSerialNumber { get; init; }
	[DataMember(Order = 6)]
	public required string? SerialNumber { get; init; }
}

[DataContract]
public sealed class MonitorResponse : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ResponseStatus Status { get; init; }
	[DataMember(Order = 3)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorReleaseRequest
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorVcpGetRequest : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required uint MonitorHandle { get; init; }
	[DataMember(Order = 3)]
	public required byte VcpCode { get; init; }
}

[DataContract]
public sealed class MonitorVcpGetResponse : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ResponseStatus Status { get; init; }
	[DataMember(Order = 3)]
	public ushort CurrentValue { get; init; }
	[DataMember(Order = 4)]
	public ushort MaximumValue { get; init; }
	[DataMember(Order = 5)]
	public bool IsTemporary { get; init; }
}

[DataContract]
public sealed class MonitorVcpSetRequest : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required uint MonitorHandle { get; init; }
	[DataMember(Order = 3)]
	public required byte VcpCode { get; init; }
	[DataMember(Order = 4)]
	public required ushort Value { get; init; }
}

[DataContract]
public sealed class MonitorVcpSetResponse : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ResponseStatus Status { get; init; }
}

[DataContract]
public sealed class MonitorCapabilitiesRequest : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorCapabilitiesResponse : IRequestId
{
	[DataMember(Order = 1)]
	public required int RequestId { get; init; }
	[DataMember(Order = 2)]
	public required ResponseStatus Status { get; init; }
	[DataMember(Order = 4)]
	public required ImmutableArray<byte> Utf8Capabilities { get; init; }
}
