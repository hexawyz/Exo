namespace Exo.Service;

public class UnsupportedSettingException : Exception
{
	public MonitorSetting MonitorSetting { get; }

	public UnsupportedSettingException() : this("The setting is not supported.")
	{
	}

	public UnsupportedSettingException(MonitorSetting monitorSetting) : this(monitorSetting, $"The setting {monitorSetting} is not supported.")
	{
	}

	public UnsupportedSettingException(string? message) : base(message)
	{
	}

	public UnsupportedSettingException(MonitorSetting monitorSetting, string? message) : base(message)
	{
		MonitorSetting = monitorSetting;
	}
}

