using Exo.Settings.Ui.Services;

namespace Exo.Settings.Ui.Ipc;

internal interface IServiceControl: IMenuItemInvoker, IPowerService, IMonitorService, ISensorService, ILightingService
{
}
