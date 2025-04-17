namespace Exo.Service;

public sealed class DpiChangeNotification
{
	public required Guid DeviceId { get; init; }
	public required byte? PresetIndex { get; init; }
	public required DotsPerInch Dpi { get; init; }
}
