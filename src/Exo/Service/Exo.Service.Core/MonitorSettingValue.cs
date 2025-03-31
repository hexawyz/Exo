namespace Exo.Service;

public readonly record struct MonitorSettingValue
{
	public MonitorSettingValue(Guid deviceId, MonitorSetting setting, ushort currentValue, ushort minimumValue, ushort maximumValue)
	{
		DeviceId = deviceId;
		Setting = setting;
		CurrentValue = currentValue;
		MinimumValue = minimumValue;
		MaximumValue = maximumValue;
	}

	public Guid DeviceId { get; }
	public MonitorSetting Setting { get; }
	public ushort CurrentValue { get; }
	public ushort MinimumValue { get; }
	public ushort MaximumValue { get; }
}
