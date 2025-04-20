using Exo.Settings.Ui.Services;

namespace Exo.Service.Ipc;

internal interface IServiceControl: IMenuItemInvoker, IImageService, IPowerService, IMouseService, IMonitorService, ISensorService, ILightingService, IEmbeddedMonitorService, ILightService, ICoolingService
{
}
