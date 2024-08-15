using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct MouseDpiPresetsInformation
{
	public required Guid DeviceId { get; init; }
	public required byte? ActivePresetIndex { get; init; }
	public required ImmutableArray<DotsPerInch> DpiPresets { get; init; }
}
