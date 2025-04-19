using System.Collections.Immutable;
using Exo.EmbeddedMonitors;
using Exo.Images;
using Exo.Monitors;

namespace Exo.Service;

public readonly struct EmbeddedMonitorInformation(Guid monitorId, MonitorShape shape, ImageRotation defaultRotation, Size imageSize, PixelFormat pixelFormat, ImageFormats supportedImageFormats, EmbeddedMonitorCapabilities capabilities, ImmutableArray<EmbeddedMonitorGraphicsDescription> supportedGraphics)
{
	public Guid MonitorId { get; } = monitorId;
	public MonitorShape Shape { get; } = shape;
	public ImageRotation DefaultRotation { get; } = defaultRotation;
	public Size ImageSize { get; } = imageSize;
	public PixelFormat PixelFormat { get; } = pixelFormat;
	public ImageFormats SupportedImageFormats { get; } = supportedImageFormats;
	public EmbeddedMonitorCapabilities Capabilities { get; } = capabilities;
	public ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; } = supportedGraphics;
}
