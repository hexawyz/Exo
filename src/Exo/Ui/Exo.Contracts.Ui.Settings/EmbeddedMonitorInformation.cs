using System.Collections.Immutable;
using System.Runtime.Serialization;

namespace Exo.Contracts.Ui.Settings;

[DataContract]
public sealed class EmbeddedMonitorInformation
{
	[DataMember(Order = 1)]
	public required Guid MonitorId { get; init; }
	[DataMember(Order = 2)]
	public MonitorShape Shape { get; init; }
	[DataMember(Order = 3)]
	public Size ImageSize { get; init; }
	//[DataMember(Order = 4)]
	//public PixelFormat PixelFormat { get; init; }
	[DataMember(Order = 5)]
	public ImageFormats SupportedImageFormats { get; init; }
	[DataMember(Order = 6)]
	public EmbeddedMonitorCapabilities Capabilities { get; init; }
	[DataMember(Order = 7)]
	public ImmutableArray<EmbeddedMonitorGraphicsDescription> SupportedGraphics { get; init; }
}
