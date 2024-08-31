using System.Collections.Immutable;
using DeviceTools.DisplayDevices.Mccs;

namespace Exo.Service;

/// <summary>Defines the internal control interface for monitors.</summary>
/// <remarks>This will be implemented by the monitor proxy service.</remarks>
internal interface IMonitorControlService
{
	Task<IMonitorControlAdapter> ResolveAdapterAsync(string deviceName, CancellationToken cancellationToken);
}

internal interface IMonitorControlAdapter
{
	Task<IMonitorControlMonitor> ResolveMonitorAsync(ushort vendorId, ushort productId, uint idSerialNumber, string? serialNumber, CancellationToken cancellationToken);
}

internal interface IMonitorControlMonitor : IAsyncDisposable
{
	Task<ImmutableArray<byte>> GetCapabilitiesAsync(CancellationToken cancellationToken);
	Task<VcpFeatureReply> GetVcpFeatureAsync(byte vcpCode, CancellationToken cancellationToken);
	Task SetVcpFeatureAsync(byte vcpCode, ushort value, CancellationToken cancellationToken);
}
