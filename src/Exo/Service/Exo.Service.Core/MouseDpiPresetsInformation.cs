using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct MouseDpiPresetsInformation(Guid deviceId, byte? activePresetIndex, ImmutableArray<DotsPerInch> dpiPresets)
{
	public Guid DeviceId { get; } = deviceId;
	public byte? ActivePresetIndex { get; } = activePresetIndex;
	public ImmutableArray<DotsPerInch> DpiPresets { get; } = dpiPresets;
}
