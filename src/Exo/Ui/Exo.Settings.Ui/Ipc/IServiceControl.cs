using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.Ipc;

internal interface IServiceControl: IMenuItemInvoker, IImageService, IPowerService, IMouseService, IMonitorService, ISensorService, ILightingService, IEmbeddedMonitorService, ILightService
{
}
