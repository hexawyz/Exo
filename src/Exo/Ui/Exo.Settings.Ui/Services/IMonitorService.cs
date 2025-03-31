using Exo.Service;

namespace Exo.Settings.Ui.Services;

internal interface IMonitorService
{
	ValueTask SetSettingValueAsync(Guid deviceId, MonitorSetting setting, ushort value, CancellationToken cancellationToken);

	ValueTask RefreshMonitorSettingsAsync(Guid deviceId, CancellationToken cancellationToken);
}
