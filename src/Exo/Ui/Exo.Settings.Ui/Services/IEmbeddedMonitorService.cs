using Exo.Images;

namespace Exo.Settings.Ui.Services;

internal interface IEmbeddedMonitorService
{
	ValueTask SetBuiltInGraphicsAsync(Guid deviceId, Guid monitorId, Guid graphicsId, CancellationToken cancellationToken);
	ValueTask SetImageAsync(Guid deviceId, Guid monitorId, UInt128 imageId, Rectangle cropRegion, CancellationToken cancellationToken);
}
