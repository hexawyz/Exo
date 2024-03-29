using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Exo.Contracts.Ui.Settings;

namespace Exo.Service.Services;

internal sealed class GrpcMonitorService : IMonitorService
{
	private readonly MonitorService _monitorService;

	public GrpcMonitorService(MonitorService monitorService) => _monitorService = monitorService;

	public async IAsyncEnumerable<MonitorSettingValue> WatchSettingsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var notification in _monitorService.WatchSettingsAsync(cancellationToken).ConfigureAwait(false))
		{
			yield return new()
			{
				DeviceId = notification.DeviceId,
				Setting = notification.Setting.ToGrpc(),
				CurrentValue = notification.CurrentValue,
				MinimumValue = notification.MinimumValue,
				MaximumValue = notification.MaximumValue,
			};
		}
	}

	public async ValueTask<MonitorSupportedSettings> GetSupportedSettingsAsync(DeviceRequest request, CancellationToken cancellationToken)
		=> new()
		{
			Settings = ImmutableArray.CreateRange
			(
				await _monitorService.GetSupportedSettingsAsync(request.Id, cancellationToken).ConfigureAwait(false),
				setting => setting.ToGrpc()
			),
		};

	public ValueTask SetSettingValueAsync(MonitorSettingUpdate request, CancellationToken cancellationToken)
		=> _monitorService.SetSettingValueAsync(request.DeviceId, request.Setting.FromGrpc(), request.Value, cancellationToken);

	public ValueTask SetBrightnessAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken)
		=> _monitorService.SetBrightnessAsync(request.DeviceId, request.Value, cancellationToken);

	public ValueTask SetContrastAsync(MonitorSettingDirectUpdate request, CancellationToken cancellationToken)
		=> _monitorService.SetContrastAsync(request.DeviceId, request.Value, cancellationToken);
}
