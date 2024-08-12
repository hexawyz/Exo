using System.Collections.Immutable;

namespace Exo.Service;

internal readonly struct MouseDpiPresetsInformation
{
	public Guid DeviceId { get; init; }
	public byte? ActivePresetIndex { get; init; }
	public ImmutableArray<DotsPerInch> DpiPresets { get; init; }
}
