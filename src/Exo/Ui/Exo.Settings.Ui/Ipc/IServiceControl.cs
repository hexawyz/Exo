using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.Ipc;

internal interface IServiceControl: IMenuItemInvoker, IPowerService, IMouseService, IMonitorService, ISensorService, ILightingService
{
}
