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
	[OperationContract(Name = "ProcessRequests")]
	IAsyncEnumerable<MonitorControlProxyRequest> ProcessRequestsAsync(IAsyncEnumerable<MonitorControlProxyResponse> responses, CancellationToken cancellationToken);
}

public enum MonitorControlResponseStatus : byte
{
	Success = 0,
	NotFound = 1,
	Error = 2,
	InvalidVcpCode = 3,
}

public enum MonitorControlProxyRequestResponseOneOfCase
{
	None = 0,
	Adapter = 1,
	Monitor = 2,
	MonitorCapabilities = 3,
	MonitorVcpGet = 4,
	MonitorVcpSet = 5,
	MonitorRelease = 6,
}

[DataContract]
public sealed class MonitorControlProxyRequest
{
	[DataMember(Order = 1)]
	public required uint RequestId { get; init; }
	[DataMember(Order = 2)]
	public required MonitorControlProxyRequestContent Content { get; init; }
}

[DataContract]
public sealed class MonitorControlProxyResponse
{
	[DataMember(Order = 1)]
	public required uint RequestId { get; init; }
	[DataMember(Order = 2)]
	public required MonitorControlResponseStatus Status { get; init; }
	[DataMember(Order = 3)]
	public MonitorControlProxyResponseContent Content { get; init; }
}

[DataContract]
public readonly struct MonitorControlProxyRequestContent
{
	private readonly object? _value;
	private readonly MonitorControlProxyRequestResponseOneOfCase _requestType;

	public MonitorControlProxyRequestResponseOneOfCase RequestType => _requestType;

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.Adapter)]
	public AdapterRequest? AdapterRequest
	{
		get => _value as AdapterRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.Adapter); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.Monitor)]
	public MonitorRequest? MonitorRequest
	{
		get => _value as MonitorRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.Monitor); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities)]
	public MonitorCapabilitiesRequest? MonitorCapabilitiesRequest
	{
		get => _value as MonitorCapabilitiesRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet)]
	public MonitorVcpGetRequest? MonitorVcpGetRequest
	{
		get => _value as MonitorVcpGetRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet)]
	public MonitorVcpSetRequest? MonitorVcpSetRequest
	{
		get => _value as MonitorVcpSetRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorRelease)]
	public MonitorReleaseRequest? MonitorReleaseRequest
	{
		get => _value as MonitorReleaseRequest;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorRelease); }
	}

	public static implicit operator MonitorControlProxyRequestContent(AdapterRequest request)
		=> new() { AdapterRequest = request };

	public static implicit operator MonitorControlProxyRequestContent(MonitorRequest request)
		=> new() { MonitorRequest = request };

	public static implicit operator MonitorControlProxyRequestContent(MonitorCapabilitiesRequest request)
		=> new() { MonitorCapabilitiesRequest = request };

	public static implicit operator MonitorControlProxyRequestContent(MonitorVcpGetRequest request)
		=> new() { MonitorVcpGetRequest = request };

	public static implicit operator MonitorControlProxyRequestContent(MonitorVcpSetRequest request)
		=> new() { MonitorVcpSetRequest = request };

	public static implicit operator MonitorControlProxyRequestContent(MonitorReleaseRequest request)
		=> new() { MonitorReleaseRequest = request };
}

[DataContract]
public readonly struct MonitorControlProxyResponseContent
{
	private readonly object? _value;
	private readonly MonitorControlProxyRequestResponseOneOfCase _requestType;

	public MonitorControlProxyRequestResponseOneOfCase ResponseType => _requestType;

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.Adapter)]
	public AdapterResponse? AdapterResponse
	{
		get => _value as AdapterResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.Adapter); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.Monitor)]
	public MonitorResponse? MonitorResponse
	{
		get => _value as MonitorResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.Monitor); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities)]
	public MonitorCapabilitiesResponse? MonitorCapabilitiesResponse
	{
		get => _value as MonitorCapabilitiesResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet)]
	public MonitorVcpGetResponse? MonitorVcpGetResponse
	{
		get => _value as MonitorVcpGetResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet)]
	public MonitorVcpSetResponse? MonitorVcpSetResponse
	{
		get => _value as MonitorVcpSetResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet); }
	}

	[DataMember(Order = (int)MonitorControlProxyRequestResponseOneOfCase.MonitorRelease)]
	public MonitorReleaseResponse? MonitorReleaseResponse
	{
		get => _value as MonitorReleaseResponse;
		init { if (value is not null) (_value, _requestType) = (value, MonitorControlProxyRequestResponseOneOfCase.MonitorRelease); }
	}

	public static implicit operator MonitorControlProxyResponseContent(AdapterResponse request)
		=> new() { AdapterResponse = request };

	public static implicit operator MonitorControlProxyResponseContent(MonitorResponse request)
		=> new() { MonitorResponse = request };

	public static implicit operator MonitorControlProxyResponseContent(MonitorCapabilitiesResponse request)
		=> new() { MonitorCapabilitiesResponse = request };

	public static implicit operator MonitorControlProxyResponseContent(MonitorVcpGetResponse request)
		=> new() { MonitorVcpGetResponse = request };

	public static implicit operator MonitorControlProxyResponseContent(MonitorVcpSetResponse request)
		=> new() { MonitorVcpSetResponse = request };

	public static implicit operator MonitorControlProxyResponseContent(MonitorReleaseResponse request)
		=> new() { MonitorReleaseResponse = request };
}

[DataContract]
public sealed class AdapterRequest
{
	[DataMember(Order = 1)]
	public required string DeviceName { get; init; }
}

[DataContract]
public sealed class AdapterResponse
{
	[DataMember(Order = 1)]
	public required ulong AdapterId { get; init; }
}

[DataContract]
public sealed class MonitorRequest
{
	[DataMember(Order = 1)]
	public required ulong AdapterId { get; init; }
	[DataMember(Order = 2)]
	public required ushort EdidVendorId { get; init; }
	[DataMember(Order = 3)]
	public required ushort EdidProductId { get; init; }
	[DataMember(Order = 4)]
	public required uint IdSerialNumber { get; init; }
	[DataMember(Order = 5)]
	public required string? SerialNumber { get; init; }
}

[DataContract]
public sealed class MonitorResponse
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorReleaseRequest
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorReleaseResponse
{
}

[DataContract]
public sealed class MonitorVcpGetRequest
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
	[DataMember(Order = 2)]
	public required byte VcpCode { get; init; }
}

[DataContract]
public sealed class MonitorVcpGetResponse
{
	[DataMember(Order = 1)]
	public ushort CurrentValue { get; init; }
	[DataMember(Order = 2)]
	public ushort MaximumValue { get; init; }
	[DataMember(Order = 3)]
	public bool IsMomentary { get; init; }
}

[DataContract]
public sealed class MonitorVcpSetRequest
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
	[DataMember(Order = 2)]
	public required byte VcpCode { get; init; }
	[DataMember(Order = 3)]
	public required ushort Value { get; init; }
}

[DataContract]
public sealed class MonitorVcpSetResponse
{
}

[DataContract]
public sealed class MonitorCapabilitiesRequest
{
	[DataMember(Order = 1)]
	public required uint MonitorHandle { get; init; }
}

[DataContract]
public sealed class MonitorCapabilitiesResponse
{
	[DataMember(Order = 1)]
	public required ImmutableArray<byte> Utf8Capabilities { get; init; }
}
