using System.Collections.Immutable;

namespace Exo.Contracts.Ui.Overlay;

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
	MonitorAcquire = 2,
	MonitorCapabilities = 3,
	MonitorVcpGet = 4,
	MonitorVcpSet = 5,
	MonitorRelease = 6,
}

public abstract class MonitorControlProxyRequest
{
	public uint RequestId { get; }

	private protected MonitorControlProxyRequest(uint requestId) => RequestId = requestId;

	public abstract MonitorControlProxyRequestResponseOneOfCase RequestType { get; }
}

public abstract class MonitorControlProxyResponse
{
	protected MonitorControlProxyResponse(uint requestId)
	{
		RequestId = requestId;
	}

	public uint RequestId { get; }
	public abstract MonitorControlResponseStatus Status { get; }

	public abstract MonitorControlProxyRequestResponseOneOfCase ResponseType { get; }
}

public sealed class MonitorControlProxyErrorResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.None;
	public override MonitorControlResponseStatus Status { get; }

	public MonitorControlProxyErrorResponse(uint requestId, MonitorControlResponseStatus status)
		: base(requestId)
	{
		Status = status;
	}
}

public sealed class AdapterRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.Adapter;

	public string DeviceName { get; }

	public AdapterRequest(uint requestId, string deviceName) : base(requestId)
	{
		DeviceName = deviceName;
	}
}

public sealed class AdapterResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.Adapter;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public ulong AdapterId { get; }

	public AdapterResponse(uint requestId, ulong adapterId) : base(requestId)
	{
		AdapterId = adapterId;
	}
}

public sealed class MonitorAcquireRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.MonitorAcquire;

	public ulong AdapterId { get; }
	public ushort EdidVendorId { get; }
	public ushort EdidProductId { get; }
	public uint IdSerialNumber { get; }
	public string? SerialNumber { get; }

	public MonitorAcquireRequest(uint requestId, ulong adapterId, ushort edidVendorId, ushort edidProductId, uint idSerialNumber, string? serialNumber) : base(requestId)
	{
		AdapterId = adapterId;
		EdidVendorId = edidVendorId;
		EdidProductId = edidProductId;
		IdSerialNumber = idSerialNumber;
		SerialNumber = serialNumber;
	}
}

public sealed class MonitorAcquireResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.MonitorAcquire;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public uint MonitorHandle { get; }

	public MonitorAcquireResponse(uint requestId, uint monitorHandle) : base(requestId)
	{
		MonitorHandle = monitorHandle;
	}
}

public sealed class MonitorReleaseRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.MonitorRelease;

	public uint MonitorHandle { get; }

	public MonitorReleaseRequest(uint requestId, uint monitorHandle) : base(requestId)
	{
		MonitorHandle = monitorHandle;
	}
}

public sealed class MonitorReleaseResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.MonitorRelease;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public MonitorReleaseResponse(uint requestId) : base(requestId) { }
}


public sealed class MonitorVcpGetRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet;

	public uint MonitorHandle { get; }
	public byte VcpCode { get; }

	public MonitorVcpGetRequest(uint requestId, uint monitorHandle, byte vcpCode) : base(requestId)
	{
		MonitorHandle = monitorHandle;
		VcpCode = vcpCode;
	}
}

public sealed class MonitorVcpGetResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.MonitorVcpGet;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public ushort CurrentValue { get; }
	public ushort MaximumValue { get; }
	public bool IsMomentary { get; }

	public MonitorVcpGetResponse(uint requestId, ushort currentValue, ushort maximumValue, bool isMomentary) : base(requestId)
	{
		CurrentValue = currentValue;
		MaximumValue = maximumValue;
		IsMomentary = isMomentary;
	}
}

public sealed class MonitorVcpSetRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet;

	public uint MonitorHandle { get; }
	public byte VcpCode { get; }
	public ushort Value { get; }

	public MonitorVcpSetRequest(uint requestId, uint monitorHandle, byte vcpCode, ushort value) : base(requestId)
	{
		MonitorHandle = monitorHandle;
		VcpCode = vcpCode;
		Value = value;
	}
}

public sealed class MonitorVcpSetResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.MonitorVcpSet;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public MonitorVcpSetResponse(uint requestId) : base(requestId)
	{
	}
}

public sealed class MonitorCapabilitiesRequest : MonitorControlProxyRequest
{
	public override MonitorControlProxyRequestResponseOneOfCase RequestType => MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities;

	public uint MonitorHandle { get; }

	public MonitorCapabilitiesRequest(uint requestId, uint monitorHandle) : base(requestId)
	{
		MonitorHandle = monitorHandle;
	}
}

public sealed class MonitorCapabilitiesResponse : MonitorControlProxyResponse
{
	public override MonitorControlProxyRequestResponseOneOfCase ResponseType => MonitorControlProxyRequestResponseOneOfCase.MonitorCapabilities;
	public override MonitorControlResponseStatus Status => MonitorControlResponseStatus.Success;

	public ImmutableArray<byte> Utf8Capabilities { get; }

	public MonitorCapabilitiesResponse(uint requestId, ImmutableArray<byte> utf8Capabilities) : base(requestId)
	{
		Utf8Capabilities = utf8Capabilities;
	}
}
