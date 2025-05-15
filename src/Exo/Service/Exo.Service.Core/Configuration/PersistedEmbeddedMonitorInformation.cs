using System.Collections.Immutable;
using Exo.EmbeddedMonitors;
using Exo.Images;
using Exo.Monitors;

namespace Exo.Service.Configuration;

[TypeId(0xA497F88F, 0xB13F, 0x429D, 0xA3, 0x5D, 0xA3, 0x67, 0x07, 0x7B, 0x05, 0x93)]
internal readonly struct PersistedEmbeddedMonitorInformation
{
	public required MonitorShape Shape { get; init; }
	public required ImageRotation DefaultRotation { get; init; }
	public required ushort Width { get; init; }
	public required ushort Height { get; init; }
	public required PixelFormat PixelFormat { get; init; }
	public required ImageFormats ImageFormats { get; init; }
	public required EmbeddedMonitorCapabilities Capabilities { get; init; }
	public ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; init; }
}
