using System.Collections.Immutable;

namespace Exo.Settings.Ui.Services;

public interface IMouseService
{
	Task SetActiveDpiPresetAsync(Guid deviceId, byte presetIndex, CancellationToken cancellationToken);
	Task SetDpiPresetsAsync(Guid deviceId, byte activePresetIndex, ImmutableArray<DotsPerInch> presets, CancellationToken cancellationToken);
	Task SetPollingFrequencyAsync(Guid deviceId, ushort frequency, CancellationToken cancellationToken);
}
