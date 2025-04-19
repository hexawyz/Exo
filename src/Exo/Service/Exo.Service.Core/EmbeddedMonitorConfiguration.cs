using Exo.Images;

namespace Exo.Service;

public readonly struct EmbeddedMonitorConfiguration(Guid deviceId, Guid monitorId, Guid graphicsId, UInt128 imageId, Rectangle imageRegion)
{
	public Guid DeviceId { get; } = deviceId;
	public Guid MonitorId { get; } = monitorId;
	public Guid GraphicsId { get; } = graphicsId;
	public UInt128 ImageId { get; } = imageId;
	public Rectangle ImageRegion { get; } = imageRegion;
}
