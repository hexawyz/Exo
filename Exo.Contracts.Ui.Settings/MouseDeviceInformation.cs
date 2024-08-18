using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class MouseDeviceInformation
{
	[DataMember(Order = 1)]
	public required Guid DeviceId { get; init; }
	[DataMember(Order = 2)]
	public required bool IsConnected { get; init; }
	[DataMember(Order = 3)]
	public required DotsPerInch MaximumDpi { get; init; }
	[DataMember(Order = 4)]
	public required MouseCapabilities Capabilities { get; init; }
	[DataMember(Order = 5)]
	public byte MinimumDpiPresetCount { get; init; }
	[DataMember(Order = 6)]
	public byte MaximumDpiPresetCount { get; init; }
	[DataMember(Order = 7)]
	public required ImmutableArray<ushort> SupportedPollingFrequencies { get; init; }
}
