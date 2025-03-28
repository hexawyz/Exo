namespace Exo.Service;

internal readonly struct SensorConfigurationUpdate
{
	public Guid DeviceId { get; init; }
	public Guid SensorId { get; init; }
	public string? FriendlyName { get; init; }
	public bool IsFavorite { get; init; }
}
