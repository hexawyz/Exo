using Exo.Settings.Ui.Services;

namespace Exo.Service.Ipc;

internal interface IServiceControl: ICustomMenuService, IImageService, IPowerService, IMouseService, IMonitorService, ISensorService, ILightingService, IEmbeddedMonitorService, ILightService, ICoolingService
{
}
