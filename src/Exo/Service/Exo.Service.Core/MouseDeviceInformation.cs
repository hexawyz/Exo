using System.Collections.Immutable;
using Exo.Contracts;

namespace Exo.Service;

internal readonly struct MouseDeviceInformation
{
	public required Guid DeviceId { get; init; }
	public required bool IsConnected { get; init; }
	public required DotsPerInch MaximumDpi { get; init; }
	public required MouseCapabilities Capabilities { get; init; }
	public required byte MinimumDpiPresetCount { get; init; }
	public required byte MaximumDpiPresetCount { get; init; }
	public required ImmutableArray<ushort> SupportedPollingFrequencies { get; init; }
}
